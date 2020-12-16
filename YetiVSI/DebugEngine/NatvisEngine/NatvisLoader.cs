// Copyright 2020 Google LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.VisualStudio.Threading;
using YetiCommon.Util;
using YetiVSI.DebugEngine.Interfaces;
using YetiVSI.Util;

namespace YetiVSI.DebugEngine.NatvisEngine
{
    /// <summary>
    /// Provide Natvis file paths to the Natvis engine.
    /// </summary>
    public interface INatvisFileSource
    {
        /// <summary>
        /// Returns a list of Natvis file paths.
        /// </summary>
        IEnumerable<string> GetFilePaths();
    }

    public class NatvisLoader
    {
        // Used to load Natvis files from the user directory.
        const string _userDirRegKey = @"HKEY_CURRENT_USER\{0}";
        const string _userDirRegValueName = "VisualStudioLocation";
        const string _userDirSuffix = "Visualizers";

        // Used to load Natvis files from the system directory.
        const string _systemDirRegKey = @"HKEY_CURRENT_USER\{0}_Config";
        const string _systemDirRegValueName = "InstallDir";
        const string _systemDirSuffix = @"..\Packages\Debugger\Visualizers";

        const string _natvisFilePattern = "*.natvis";

        readonly JoinableTaskContext _taskContext;
        readonly ITaskExecutor _taskExecutor;
        readonly NatvisDiagnosticLogger _logger;

        readonly INatvisFileSource _solutionNatvisFiles;
        readonly NatvisValidator.Factory _validatorFactory;
        readonly IWindowsRegistry _winRegistry;
        readonly IFileSystem _fileSystem;

        string _registryRoot;

        public NatvisLoader(JoinableTaskContext taskContext, ITaskExecutor taskExecutor,
                            NatvisDiagnosticLogger logger, INatvisFileSource solutionNatvisFiles,
                            NatvisValidator.Factory validatorFactory, IWindowsRegistry winRegistry,
                            IFileSystem fileSystem)
        {
            _taskContext = taskContext;
            _taskExecutor = taskExecutor;
            _logger = logger;
            _solutionNatvisFiles = solutionNatvisFiles;
            _validatorFactory = validatorFactory;
            _winRegistry = winRegistry;
            _fileSystem = fileSystem;
        }

        /// <summary>
        /// Reloads Natvis files from the project and registry defined locations.
        /// </summary>
        /// <param name="typeVisualizers">Type visualizers to initialize.</param>
        public void Reload(ICollection<NatvisVisualizerScanner.FileInfo> typeVisualizers)
        {
            _taskContext.ThrowIfNotOnMainThread();

            // Submitting to the task executor to prevent natvis from reloading when asynchronous
            // operations (possibly expressions evaluation) are in progress.
            _taskExecutor.Run(() =>
            {
                LoadProjectFiles(typeVisualizers);
                if (_registryRoot != null)
                {
                    LoadFromRegistry(_registryRoot, typeVisualizers);
                }
            });
        }

        /// <summary>
        /// Attempts to load Natvis files from project files.
        /// </summary>
        /// <param name="typeVisualizers">Type visualizers to initialize.</param>
        public void LoadProjectFiles(ICollection<NatvisVisualizerScanner.FileInfo> typeVisualizers)
        {
            _taskContext.ThrowIfNotOnMainThread();

            try
            {
                TraceWriteLine(NatvisLoggingLevel.VERBOSE,
                               "Loading Natvis files found in the project...");
                var sw = new Stopwatch();
                sw.Start();
                _solutionNatvisFiles.GetFilePaths()
                    .ForEach(path => LoadFile(path, typeVisualizers));
                sw.Stop();
                TraceWriteLine(NatvisLoggingLevel.VERBOSE,
                               $"Loaded project Natvis files in {sw.Elapsed}.");
            }
            catch (FileNotFoundException ex)
            {
                TraceWriteLine(NatvisLoggingLevel.ERROR,
                               $"Failed to load Natvis files from project. Reason: {ex.Message}" +
                               $"{Environment.NewLine}Stacktrace:{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Attempts to load Natvis files from the user and system directories found in the Windows
        /// Registry.
        /// File loading failures will be logged.
        /// </summary>
        /// <param name="registryRoot">Registry root.</param>
        /// <param name="typeVisualizers">Type visualizers to initialize.</param>
        public void LoadFromRegistry(string registryRoot,
                                     ICollection<NatvisVisualizerScanner.FileInfo> typeVisualizers)
        {
            _registryRoot = registryRoot;

            // User directory.
            string userKey = string.Format(_userDirRegKey, registryRoot);
            if (!LoadFromRegistryKey(userKey, _userDirRegValueName, _userDirSuffix,
                                     typeVisualizers))
            {
                TraceWriteLine(NatvisLoggingLevel.ERROR,
                               "Failed to load Natvis files from the user directory." +
                               $" Registry key not found: {userKey}:{_userDirRegValueName}");
            }

            // System directory.
            string systemKey = string.Format(_systemDirRegKey, registryRoot);
            if (!LoadFromRegistryKey(systemKey, _systemDirRegValueName, _systemDirSuffix,
                                     typeVisualizers))
            {
                TraceWriteLine(NatvisLoggingLevel.ERROR,
                               "Failed to load Natvis files from the system directory." +
                               $" Registry key not found: {systemKey}:{_systemDirRegValueName}");
            }
        }

        /// <summary>
        /// Loads Natvis files from file.
        /// </summary>
        /// <param name="filePath">File path.</param>
        /// <param name="typeVisualizers">Type visualizers to initialize.</param>
        public void LoadFile(string filePath,
                             ICollection<NatvisVisualizerScanner.FileInfo> typeVisualizers)
        {
            try
            {
                if (!_fileSystem.File.Exists(filePath))
                {
                    TraceWriteLine(NatvisLoggingLevel.ERROR,
                                   "Unable to load Natvis file because it doesn't exist." +
                                   $" '{filePath}'");
                    return;
                }

                TraceWriteLine(NatvisLoggingLevel.VERBOSE, $"Loading Natvis file '{filePath}'.");

                var sw = new Stopwatch();
                sw.Start();

                using (Stream stream =
                    _fileSystem.FileStream.Create(filePath, FileMode.Open, FileAccess.Read))
                {
                    LoadFromStream(stream, filePath, typeVisualizers);
                }

                NatvisValidator validator = _validatorFactory.Create();
                validator.Validate(filePath);

                sw.Stop();
                TraceWriteLine(NatvisLoggingLevel.VERBOSE,
                               $"Loaded Natvis file '{filePath}' in {sw.Elapsed}.");
            }
            catch (InvalidOperationException ex)
            {
                // Handles invalid XML errors.

                // don't allow natvis failures to stop debugging
                TraceWriteLine(NatvisLoggingLevel.ERROR,
                               $"Failed to load Natvis file '{filePath}'." +
                               $" Reason: {ex.Message}");
            }
            catch (Exception ex)
            {
                // TODO: Ensure 'unhandled' exceptions are logged at a higher level, such
                // as a global error handler.
                TraceWriteLine(NatvisLoggingLevel.ERROR,
                               $"Failed to load Natvis file '{filePath}'. Reason: {ex.Message}" +
                               $"{Environment.NewLine}Stacktrace:{ex.StackTrace}");

                throw;
            }
        }

        /// <summary>
        /// Loads Natvis files from string. This method is used in tests.
        /// </summary>
        /// <param name="natvisText">String with Natvis specification.</param>
        /// <param name="typeVisualizers">Type visualizers to initialize.</param>
        public void LoadFromString(string natvisText,
                                   ICollection<NatvisVisualizerScanner.FileInfo> typeVisualizers)
        {
            try
            {
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(natvisText)))
                {
                    LoadFromStream(stream, "<From String>", typeVisualizers);
                }

                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(natvisText)))
                {
                    NatvisValidator validator = _validatorFactory.Create();
                    validator.Validate(stream);
                }
            }
            catch (InvalidOperationException ex)
            {
                // Handles invalid XML errors.

                // don't allow natvis failures to stop debugging
                TraceWriteLine(NatvisLoggingLevel.ERROR,
                               $"Failed to load Natvis text. Reason: {ex.Message}" +
                               $"{Environment.NewLine}Stacktrace:{ex.StackTrace}");
            }
            catch (Exception ex)
            {
                // TODO: Ensure 'unhandled' exceptions are logged at a higher level, such
                // as a global error handler.
                _logger.Error(
                    $"Failed to load Natvis text. Reason: {ex.Message}" +
                    $"{Environment.NewLine}Text:{Environment.NewLine}{natvisText}" +
                    $"{Environment.NewLine}Stacktrace:{Environment.NewLine}{ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Attempts to load Natvis files from a directory resolved from the Windows Registry.
        /// </summary>
        /// <param name="keyName">The registry key name.</param>
        /// <param name="valueName">The registry value name.</param>
        /// <param name="dirSuffix">The directory suffix to append to the path found in the
        /// registry.</param>
        /// <param name="typeVisualizers">Type visualizers to initialize.</param>
        /// <returns>False if the registry key doesn't exist.</returns>
        bool LoadFromRegistryKey(string keyName, string valueName, string dirSuffix,
                                 ICollection<NatvisVisualizerScanner.FileInfo> typeVisualizers)
        {
            if (_winRegistry == null)
            {
                throw new InvalidOperationException("winRegistry cannot be null.");
            }

            string natvisDir = (string) _winRegistry.GetValue(keyName, valueName, null);
            if (string.IsNullOrEmpty(natvisDir))
            {
                return false;
            }

            natvisDir = Path.GetFullPath(Path.Combine(natvisDir, dirSuffix));
            LoadFromDirectory(natvisDir, typeVisualizers);
            return true;
        }

        /// <summary>
        /// Loads all natvis files found in a directory and sub-directories.
        /// </summary>
        /// <param name="loadDir">The directory path to load files from.</param>
        /// <param name="typeVisualizers">Type visualizers to initialize.</param>
        void LoadFromDirectory(string loadDir,
                               ICollection<NatvisVisualizerScanner.FileInfo> typeVisualizers)
        {
            try
            {
                TraceWriteLine(NatvisLoggingLevel.VERBOSE,
                               $"Loading Natvis files found in {loadDir}...");
                var sw = new Stopwatch();
                sw.Start();

                foreach (string file in _fileSystem.Directory.EnumerateFiles(
                    loadDir, _natvisFilePattern, SearchOption.AllDirectories))
                {
                    LoadFile(file, typeVisualizers);
                }

                sw.Stop();
                TraceWriteLine(NatvisLoggingLevel.VERBOSE,
                               $"Loaded Natvis files from {loadDir} in {sw.Elapsed}.");
            }
            catch (DirectoryNotFoundException ex)
            {
                TraceWriteLine(NatvisLoggingLevel.ERROR,
                               $"Failed to load Natvis files from {loadDir}." +
                               $" Reason: {ex.Message}.");
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Microsoft.Security.Xml", "CA3053: UseSecureXmlResolver.",
            Justification =
                "Usage is secure -- XmlResolver property is set to 'null' in desktop " +
                "CLR, and is always null in CoreCLR. But CodeAnalysis cannot understand the " +
                "invocation since it happens through reflection.")]
        bool LoadFromStream(Stream stream, string filename,
                            ICollection<NatvisVisualizerScanner.FileInfo> typeVisualizers)
        {
            var serializer = new XmlSerializer(typeof(AutoVisualizer));
            var settings = new XmlReaderSettings
            {
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                IgnoreWhitespace = true
            };

            // set XmlResolver via reflection, if it exists. This is required for desktop CLR, as
            // otherwise the XML reader may attempt to hit untrusted external resources.
            PropertyInfo xmlResolverProperty = settings.GetType().GetProperty("XmlResolver",
                                                                              BindingFlags.Public |
                                                                              BindingFlags
                                                                                  .Instance);
            xmlResolverProperty?.SetValue(settings, null);

            using (var reader = XmlReader.Create(stream, settings))
            {
                var autoVis = serializer.Deserialize(reader) as AutoVisualizer;
                if (autoVis?.Items == null)
                {
                    return false;
                }

                var f = new NatvisVisualizerScanner.FileInfo(autoVis, filename);
                foreach (object o in autoVis.Items)
                {
                    if (o is VisualizerType)
                    {
                        var v = (VisualizerType) o;
                        TypeName t = TypeName.Parse(v.Name);
                        if (t != null)
                        {
                            f.Visualizers.Add(new NatvisVisualizerScanner.TypeInfo(t, v));
                        }

                        // add an entry for each alternative name too
                        if (v.AlternativeType != null)
                        {
                            foreach (AlternativeTypeType a in v.AlternativeType)
                            {
                                t = TypeName.Parse(a.Name);
                                if (t != null)
                                {
                                    f.Visualizers.Add(new NatvisVisualizerScanner.TypeInfo(t, v));
                                }
                            }
                        }
                    }
                }

                typeVisualizers.Add(f);
                return true;
            }
        }

        /// <summary>
        /// Writes a log to Trace and this.logger.
        /// </summary>
        void TraceWriteLine(NatvisLoggingLevel level, string message)
        {
            if (_logger.ShouldLog(level))
            {
                _logger.Log(level, message);
            }
            else
            {
                string traceMsg = message;
                if (level == NatvisLoggingLevel.ERROR)
                {
                    traceMsg = "ERROR: " + message;
                }

                Trace.WriteLine(traceMsg);
            }
        }
    }
}
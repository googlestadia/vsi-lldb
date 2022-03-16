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
using System.Linq;
using System.Text;
using DebuggerApi;
using Microsoft.VisualStudio.Threading;
using YetiVSI.DebugEngine.Variables;
using YetiVSI.Util;

namespace YetiVSI.DebugEngine.NatvisEngine
{
    public class NatvisVisualizerScanner
    {
        public class TypeInfo
        {
            public TypeName ParsedName { get; }
            public VisualizerType Visualizer { get; }

            public TypeInfo(TypeName name, VisualizerType visualizer)
            {
                ParsedName = name;
                Visualizer = visualizer;
            }
        }

        public class FileInfo
        {
            public List<TypeInfo> Visualizers { get; }
            public readonly AutoVisualizer Environment;
            public readonly string Filename;

            public FileInfo(AutoVisualizer env, string filename)
            {
                Environment = env;
                Visualizers = new List<TypeInfo>();
                Filename = filename;
            }
        }

        // The maximum number of ancestor types to check when finding a visualizer.
        const int _maxInheritedTypes = 100;

        IDictionary<string, VisualizerInfo> _visualizerCache;
        IList<FileInfo> _typeVisualizers;
        bool _enableStringVisualizer = false;

        readonly NatvisDiagnosticLogger _logger;
        readonly NatvisLoader _natvisLoader;
        readonly JoinableTaskContext _taskContext;
        private RemoteTarget _target;
        // Should we load the entire natvis (or just built-in visualizers)?
        readonly bool _loadEntireNatvis = false;

        public NatvisVisualizerScanner(NatvisDiagnosticLogger logger, NatvisLoader natvisLoader,
                                       JoinableTaskContext taskContext, bool loadEntireNatvis)
        {
            _logger = logger;
            _natvisLoader = natvisLoader;
            _taskContext = taskContext;
            _loadEntireNatvis = loadEntireNatvis;

            InitDataStructures();
        }

        #region NatvisLoading

        /// <summary>
        /// Reload Natvis files.
        /// </summary>
        public void Reload(TextWriter writer = null)
        {
            _taskContext.ThrowIfNotOnMainThread();

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // Initialize structures and load built-in visualizers.
            InitDataStructures();
            // Load Natvis files (from registry root and project).
            // TODO: Consider handling changes to this option in runtime.
            if (_loadEntireNatvis)
            {
                _natvisLoader.Reload(_typeVisualizers);
            }

            stopwatch.Stop();
            writer?.WriteLine($"Success!!! Took {stopwatch.Elapsed}. See the Natvis pane of the " +
                              "output window for info.");
        }

        /// <summary>
        /// Load Natvis files from string.
        /// </summary>
        public void LoadFromString(string natvisText)
        {
            _natvisLoader.LoadFromString(natvisText, _typeVisualizers);
        }

        /// <summary>
        /// Load Natvis files from registry.
        /// </summary>
        public void SetRegistryRoot(string registryRoot)
        {
            _natvisLoader.SetRegistryRoot(registryRoot);
        }

        public void SetTarget(RemoteTarget target)
        {
            _target = target;
        }

#endregion

        /// <summary>
        /// Outputs some statistics to the Natvis diagnostic logger and log files.
        /// </summary>
        public void LogStats(TextWriter writer, int verbosityLevel = 0)
        {
            const string lineBreak = "===========================================================";

            int totalVizCount = _typeVisualizers.Sum(viz => viz.Visualizers.Count);

            writer.WriteLine();
            writer.WriteLine($"typeVisualizers.Count = {_typeVisualizers.Count}");
            writer.WriteLine($"vizCache.Count = {_visualizerCache.Count}");

            writer.WriteLine($"Total Visualizer Count = {totalVizCount}");

            if (verbosityLevel >= 2)
            {
                writer.WriteLine();
                writer.WriteLine($"Loaded Natvis files ({_typeVisualizers.Count}):");
                foreach (FileInfo fileInfo in _typeVisualizers)
                {
                    writer.WriteLine($"File: {fileInfo.Filename}");
                    writer.WriteLine($"Count: {fileInfo.Visualizers.Count}");
                    writer.WriteLine(lineBreak);
                    foreach (TypeInfo typeInfo in fileInfo.Visualizers)
                    {
                        writer.WriteLine($"Name: {typeInfo.Visualizer.Name}" +
                                         $"\t\tPriority: {typeInfo.Visualizer.Priority}");
                    }

                    writer.WriteLine();
                }
            }

            if (verbosityLevel >= 1)
            {
                writer.WriteLine();
                writer.WriteLine($"Cached Visualizers ({_visualizerCache.Count})");
                writer.WriteLine(lineBreak);
                if (_visualizerCache.Any())
                {
                    foreach (var cachedVisualizer in _visualizerCache)
                    {
                        writer.WriteLine($"Lookup type: {cachedVisualizer.Key}" +
                                         $"\t\tResolved Type: " +
                                         $"{cachedVisualizer.Value?.Visualizer.Name}");
                    }

                    writer.WriteLine();
                }
            }

            writer.WriteLine();
        }

        /// <summary>
        /// Find Natvis visualizer by variable.
        /// </summary>
        public VisualizerInfo FindType(IVariableInformation variable)
        {
            // Check for custom visualizers first.
            if (variable.CustomVisualizer != CustomVisualizer.None)
            {
                // Custom visualizers are keyed by pseudo-types "$" + CustomVisualizer enum name.
                string pseudoTypeName = "$" + variable.CustomVisualizer;

                VisualizerInfo visualizer;
                if (_visualizerCache.TryGetValue(pseudoTypeName, out visualizer))
                {
                    _logger.Verbose(() => $"Selected cached custom Natvis Visualizer " +
                                        $"'{visualizer?.Visualizer.Name ?? "null"} '" +
                                        $"for custom visualizer '{variable.CustomVisualizer}'");

                    return visualizer;
                }

                visualizer = Scan(pseudoTypeName, TypeName.Parse(pseudoTypeName));
                if (visualizer != null)
                {
                    _logger.Verbose(() => $"Selected Natvis Visualizer " +
                                        $"'{visualizer.Visualizer.Name}'" +
                                        $" for custom visualizer '{variable.CustomVisualizer}'");

                    return visualizer;
                }
            }

            string initialTypeName = variable.TypeName;

            _logger.Verbose($"Finding Natvis Visualizer for type '{initialTypeName}'");
            if (_visualizerCache.ContainsKey(initialTypeName))
            {
                VisualizerInfo visualizer = _visualizerCache[variable.TypeName];
                _logger.Verbose(() => $"Selected cached Natvis Visualizer " +
                                    $"'{visualizer?.Visualizer.Name ?? "null"}' for type " +
                                    $"'{initialTypeName}'");

                return visualizer;
            }

            uint count = 0;
            foreach (SbType type in variable.GetAllInheritedTypes())
            {
                VisualizerInfo visualizer = ScanForAliasOrCanonicalType(initialTypeName, type);
                if (visualizer != null)
                {
                    _logger.Verbose(
                        () => $"Selected Natvis Visualizer '{visualizer.Visualizer.Name}'" +
                            $" for type '{initialTypeName}'");

                    return visualizer;
                }

                ++count;

                // Safety check to make sure we don't get in to an expensive loop.
                if (count > _maxInheritedTypes)
                {
                    _logger.Warning($"The '{initialTypeName}' type exceeds the" +
                                    $" maximum number ({_maxInheritedTypes}) of searchable base " +
                                    $"classes.");

                    break;
                }
            }

            _logger.Verbose($"No Natvis Visualizer found for type '{initialTypeName}'");
            return null;
        }

        private VisualizerInfo ScanForAliasOrCanonicalType(string initialTypeName, SbType type)
        {
            var typeName = type.GetName();
            var parsedName = TypeName.Parse(typeName);
            if (parsedName == null)
            {
                return null;
            }

            _logger.Verbose(
                () => $"Scanning for Natvis Visualizer for type '{parsedName.BaseName}' for " +
                      $"variable of type '{initialTypeName}'");

            VisualizerInfo visualizer = Scan(initialTypeName, parsedName);
            if (visualizer != null)
            {
                return visualizer;
            }

            // Fallback to canonical type. `typeName` could be alias for another type (e.g.
            // `using MyType = CanonicalType` or `typedef CanonicalType MyType`). If there's no
            // visualizer for the given type, try to find a visualizer for its canonical type.
            // NOTE: This Natvis implementation differs from Visual Studio's native Natvis, which
            // doesn't support type aliases (it canonicalizes types by default).
            var canonicalTypeName = type.GetCanonicalType().GetName();
            if (canonicalTypeName == typeName)
            {
                // The given type is canonicalized. There is no need for additional scan.
                return null;
            }

            var parsedCanonicalName = TypeName.Parse(canonicalTypeName);
            if (parsedCanonicalName == null)
            {
                return null;
            }

            _logger.Verbose(() => $"Scanning for Natvis Visualizer for canonical type " +
                                  $"'{parsedCanonicalName.BaseName}' for variable of type " +
                                  $"'{initialTypeName}'");

            return Scan(initialTypeName, parsedCanonicalName);
        }

        void InitDataStructures()
        {
            _typeVisualizers = new List<FileInfo>();
            _visualizerCache = new Dictionary<string, VisualizerInfo>();
            CreateCustomVisualizers();
        }

        VisualizerInfo Scan(string varTypeName, TypeName typeNameToFind)
        {
            // Iterate list in reverse order, so that files loaded later take precedence.
            // In particular, CustomVisualizers can be overridden by user files.
            // TODO: Ordering is a bit brittle, consider using another way to make
            // CustomVisualizers overridable, e.g. priority, a custom built-in flag or storing
            // built-in Natvis in a file that can be changed by users.
            TypeInfo bestMatch = null;
            TypeName.MatchScore bestScore = null;
            for (int index = _typeVisualizers.Count - 1; index >= 0; --index)
            {
                FileInfo fileInfo = _typeVisualizers[index];

                // TODO: match on version, etc
                foreach (TypeInfo v in fileInfo.Visualizers)
                {
                    var score = new TypeName.MatchScore();
                    if (v.ParsedName.Match(typeNameToFind, score))
                    {
                        if (bestScore == null || score.CompareTo(bestScore) > 0)
                        {
                            bestScore = score;
                            bestMatch = v;
                        }
                    }
                }
            }

            _visualizerCache[varTypeName] =
                bestMatch != null ? new VisualizerInfo(bestMatch, typeNameToFind) : null;

            return _visualizerCache[varTypeName];
        }

        public void EnableStringVisualizer()
        {
            _enableStringVisualizer = true;
        }

        void CreateCustomVisualizers()
        {
            string xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
    <Type Name=""$SSE"">
        <Expand HideRawView=""true"">
            <Item Name=""xmm0f"">xmm0,vf32</Item>
            <Item Name=""xmm1f"">xmm1,vf32</Item>
            <Item Name=""xmm2f"">xmm2,vf32</Item>
            <Item Name=""xmm3f"">xmm3,vf32</Item>
            <Item Name=""xmm4f"">xmm4,vf32</Item>
            <Item Name=""xmm5f"">xmm5,vf32</Item>
            <Item Name=""xmm6f"">xmm6,vf32</Item>
            <Item Name=""xmm7f"">xmm7,vf32</Item>
        </Expand>
    </Type>

    <Type Name=""$SSE2"">
        <Expand HideRawView=""true"">
            <Item Name=""xmm0d"">xmm0,vf64</Item>
            <Item Name=""xmm1d"">xmm1,vf64</Item>
            <Item Name=""xmm2d"">xmm2,vf64</Item>
            <Item Name=""xmm3d"">xmm3,vf64</Item>
            <Item Name=""xmm4d"">xmm4,vf64</Item>
            <Item Name=""xmm5d"">xmm5,vf64</Item>
            <Item Name=""xmm6d"">xmm6,vf64</Item>
            <Item Name=""xmm7d"">xmm7,vf64</Item>
            <Item Name=""xmm8d"">xmm8,vf64</Item>
            <Item Name=""xmm9d"">xmm9,vf64</Item>
            <Item Name=""xmm10d"">xmm10,vf64</Item>
            <Item Name=""xmm11d"">xmm11,vf64</Item>
            <Item Name=""xmm12d"">xmm12,vf64</Item>
            <Item Name=""xmm13d"">xmm13,vf64</Item>
            <Item Name=""xmm14d"">xmm14,vf64</Item>
            <Item Name=""xmm15d"">xmm15,vf64</Item>

            <Item Name=""xmm8f"">xmm8,vf32</Item>
            <Item Name=""xmm9f"">xmm9,vf32</Item>
            <Item Name=""xmm10f"">xmm10,vf32</Item>
            <Item Name=""xmm11f"">xmm11,vf32</Item>
            <Item Name=""xmm12f"">xmm12,vf32</Item>
            <Item Name=""xmm13f"">xmm13,vf32</Item>
            <Item Name=""xmm14f"">xmm14,vf32</Item>
            <Item Name=""xmm15f"">xmm15,vf32</Item>
        </Expand>
    </Type>";

            if (_enableStringVisualizer)
            {
                // Debugging std::strings requires -fstandalone-debug when compiled with Clang
                // version 10 or less, which can blow up the size significantly.
                // This is a work-around that just calls c_str() on the string.
                // TODO: It doesn't fix containers of std::strings, which still freak
                // out for the earlier Clang versions.
                xml += @"
<Type Name=""std::__1::basic_string&lt;char, std::__1::char_traits&lt;char&gt;, std::__1::allocator&lt;char&gt; &gt;"">
    <DisplayString>{this->c_str(),s}</DisplayString>
    <StringView>this->c_str()</StringView>
</Type>";
            }

            // Don't forget to close the starting XML tag.
            xml += "</AutoVisualizer>";

            LoadFromString(xml);
        }
    }
}
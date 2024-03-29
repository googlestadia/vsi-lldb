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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DebuggerApi;
using Microsoft.VisualStudio.Threading;
using YetiVSI.DebugEngine.Variables;
using YetiVSI.DebuggerOptions;
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
        IDictionary<string, VisualizerInfo> _customVisualizers;
        IList<FileInfo> _typeVisualizers;
        bool _enableStringVisualizer = false;

        readonly NatvisDiagnosticLogger _logger;
        readonly NatvisLoader _natvisLoader;
        readonly JoinableTaskContext _taskContext;
        private RemoteTarget _target;
        readonly IExtensionOptions _extensionOptions;
        readonly DebuggerOptions.DebuggerOptions _debuggerOptions;

        public NatvisVisualizerScanner(NatvisDiagnosticLogger logger, NatvisLoader natvisLoader,
                                       JoinableTaskContext taskContext,
                                       IExtensionOptions extensionOptions,
                                       DebuggerOptions.DebuggerOptions debuggerOptions)
        {
            _logger = logger;
            _natvisLoader = natvisLoader;
            _taskContext = taskContext;
            _extensionOptions = extensionOptions;
            _debuggerOptions = debuggerOptions;

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
            if (LoadEntireNatvis())
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

            int totalVizCount =
                _typeVisualizers.Sum(viz => viz.Visualizers.Count) + _customVisualizers.Count;

            writer.WriteLine();
            writer.WriteLine($"typeVisualizers.Count = {_typeVisualizers.Count}");
            writer.WriteLine($"customVisualizers.Count = {_customVisualizers.Count}");
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
        public async Task<VisualizerInfo> FindTypeAsync(IVariableInformation variable)
        {
            // Check for custom visualizers first.
            if (variable.CustomVisualizer != CustomVisualizer.None)
            {
                // Custom visualizers are keyed by pseudo-types "$" + CustomVisualizer enum name.
                string pseudoTypeName = "$" + variable.CustomVisualizer;

                VisualizerInfo visualizer;
                if (_customVisualizers.TryGetValue(pseudoTypeName, out visualizer))
                {
                    _logger.Verbose(() => $"Selected cached custom Natvis Visualizer " +
                                        $"'{visualizer?.Visualizer.Name ?? "null"} '" +
                                        $"for custom visualizer '{variable.CustomVisualizer}'");

                    return visualizer;
                }

                // This isn't expected to happen. Custom visualizer should be loaded at this point.
                _logger.Error($"Could not find custom Natvis visualizer '{pseudoTypeName}'");

                // Users are not supposed to override custom visualizers, we can return.
                return null;
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

            // TODO: The following if checks for dereferencing the variable are not
            // really needed. However, LLDB doesn't handle dereferencing types of dynamic values
            // properly, but this solution turned out to work fine. We should delete these checks
            // after the bug in LLDB is fixed.
            if (variable.IsReference || variable.IsPointer)
            {
                variable = variable.Dereference();
            }
            if (variable == null || variable.IsPointer)
            {
                // Visualizers are not used on pointer-to-pointer variables.
                return null;
            }

            uint count = 0;
            foreach (SbType type in variable.GetAllInheritedTypes())
            {
                VisualizerInfo visualizer =
                    await ScanForAliasOrCanonicalTypeAsync(initialTypeName, type);
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

        private async Task<VisualizerInfo> ScanForAliasOrCanonicalTypeAsync(string initialTypeName,
                                                                            SbType type)
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

            VisualizerInfo visualizer = await ScanAsync(initialTypeName, parsedName, type);
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

            return await ScanAsync(initialTypeName, parsedCanonicalName, type.GetCanonicalType());
        }

        void InitDataStructures()
        {
            _typeVisualizers = new List<FileInfo>();
            _visualizerCache = new Dictionary<string, VisualizerInfo>();
            _customVisualizers = new Dictionary<string, VisualizerInfo>();
            CreateCustomVisualizers();
            CreateDefaultVisualizers();
        }

        public void InvalidateCache()
        {
            _visualizerCache.Clear();
        }

        async Task<VisualizerInfo> ScanAsync(string varTypeName, TypeName typeNameToFind,
                                             SbType sbType)
        {
            // Iterate list in reverse order, so that files loaded later take precedence.
            // In particular, CustomVisualizers can be overridden by user files.
            // TODO: Ordering is a bit brittle, consider using another way to make
            // CustomVisualizers overridable, e.g. priority, a custom built-in flag or storing
            // built-in Natvis in a file that can be changed by users.
            var candidates = new List<Tuple<TypeInfo, TypeName.MatchScore>>();
            for (int index = _typeVisualizers.Count - 1; index >= 0; --index)
            {
                FileInfo fileInfo = _typeVisualizers[index];

                // TODO: match on version, etc
                foreach (TypeInfo v in fileInfo.Visualizers)
                {
                    // Priority is enabled if compilation is enabled.
                    PriorityType priority =
                        IsNatvisCompilerEnabled() ? v.Visualizer.Priority : PriorityType.Medium;
                    var score = new TypeName.MatchScore(priority);
                    if (v.ParsedName.Match(typeNameToFind, score))
                    {
                        candidates.Add(Tuple.Create(v, score));
                    }
                }
            }

            if (candidates.Count == 0)
            {
                _visualizerCache[varTypeName] = null;
                return null;
            }

            // Sort candidates by score from the highest to the lowest.
            candidates.Sort((x, y) => y.Item2.CompareTo(x.Item2));

            if (IsNatvisCompilerEnabled())
            {
                var compiler = new NatvisCompiler(_target, sbType, _logger,
                                                  _extensionOptions.ExpressionEvaluationStrategy);
                foreach (var candidate in candidates)
                {
                    var vizInfo = new VisualizerInfo(candidate.Item1, typeNameToFind);
                    if (await compiler.IsCompilableAsync(vizInfo))
                    {
                        _visualizerCache[varTypeName] = vizInfo;
                        return vizInfo;
                    }

                    _logger.Verbose(
                        $"Ignoring visualizer for type '{typeNameToFind.FullyQualifiedName}' " +
                        $"labeled as '{candidate.Item1.Visualizer.Name}'.");
                }

                _visualizerCache[varTypeName] = null;
                return null;
            }

            // Compilation is disabled. Pick the first visualizer and issue warnings for
            // visualizers that could have been picked if the compiler was enabled.
            var ret = candidates.First();
            foreach (var candidate in candidates.Skip(1))
            {
                if (!candidate.Item2.Equals(ret.Item2))
                {
                    break;
                }

                _logger.Warning("Ignoring a potentially matching visualizer for type " +
                                $"'{typeNameToFind.FullyQualifiedName}' labeled as " +
                                $"'{candidate.Item1.Visualizer.Name}'. Priority attribute and " +
                                "compilation of Natvis disabled for LLDB.");
            }

            var visualizer = new VisualizerInfo(ret.Item1, typeNameToFind);
            _visualizerCache[varTypeName] = visualizer;
            return visualizer;
        }

        public void EnableStringVisualizer()
        {
            _enableStringVisualizer = true;
        }

        /// <summary>
        /// Indicates whether the entire Natvis should be loaded or just built-in visualizers.
        /// </summary>
        bool LoadEntireNatvis()
        {
            return _extensionOptions.LLDBVisualizerSupport == LLDBVisualizerSupport.ENABLED;
        }

        bool IsNatvisCompilerEnabled()
        {
            return _debuggerOptions[DebuggerOption.NATVIS_EXPERIMENTAL] ==
                       DebuggerOptionState.ENABLED &&
                   _extensionOptions.ExpressionEvaluationStrategy !=
                       ExpressionEvaluationStrategy.LLDB;
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
    </Type>
</AutoVisualizer>";

            // TODO: Consider refactoring NatvisLoader and NatvisVisualizerScanner in
            // a way that NatvisLoader loads and returns the result in appropriate form and
            // NatvisVisualizerScanner takes care of adding visualizers to a list.
            // For example, "var fileInfo = _natvisLoader.LoadFromString(...)" is cleaner.
            var sseFileList = new List<FileInfo>();
            _natvisLoader.LoadFromString(xml, sseFileList);
            foreach (var fileInfo in sseFileList)
            {
                foreach (var typeInfo in fileInfo.Visualizers)
                {
                    var pseudoTypeName = typeInfo.Visualizer.Name;
                    var vizInfo = new VisualizerInfo(typeInfo, TypeName.Parse(pseudoTypeName));
                    _customVisualizers.Add(pseudoTypeName, vizInfo);
                }
            }

            // Make sure we loaded SSE visualizers correctly.
            Debug.Assert(_customVisualizers.ContainsKey("$SSE"));
            Debug.Assert(_customVisualizers.ContainsKey("$SSE2"));
        }

        void CreateDefaultVisualizers()
        {
            if (_enableStringVisualizer)
            {
                // Debugging std::strings requires -fstandalone-debug when compiled with Clang
                // version 10 or less, which can blow up the size significantly.
                // This is a work-around that just calls c_str() on the string.
                // TODO: It doesn't fix containers of std::strings, which still freak
                // out for the earlier Clang versions.
                var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
    <Type Name=""std::__1::basic_string&lt;char, std::__1::char_traits&lt;char&gt;, std::__1::allocator&lt;char&gt; &gt;"">
        <DisplayString>{this->c_str(),s}</DisplayString>
        <StringView>this->c_str()</StringView>
    </Type>
</AutoVisualizer>";

                LoadFromString(xml);
            }
        }
    }
}
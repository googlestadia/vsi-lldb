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

﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace YetiVSI.DebugEngine.NatvisEngine
{
    public class VisualizerInfo
    {
        public VisualizerType Visualizer { get; }
        public NatvisScope NatvisScope { get; }

        public VisualizerInfo(NatvisVisualizerScanner.TypeInfo info, TypeName name)
        {
            Visualizer = info.Visualizer;

            // Resolve wildcards (i.e. find replacements for "*" template arguments).
            List<string> resolvedWildcards = new List<string>();
            ResolveWildcards(info.ParsedName, name, resolvedWildcards);
            NatvisScope = new NatvisScope();
            for (int i = 0; i < resolvedWildcards.Count; ++i)
            {
                NatvisScope.AddScopedName($"$T{i + 1}", resolvedWildcards[i]);
            }
        }

        /// <summary>
        /// Gets the ExpandType child.
        /// </summary>
        /// <returns>Returns the ExpandType child or null if it doesn't exist.</returns>
        public ExpandType GetExpandType() =>
            Visualizer.Items?.OfType<ExpandType>().FirstOrDefault();

        /// <summary>
        /// Gets the SmartPointerType child.
        /// </summary>
        /// <returns>Returns the SmartPointerType child or null if it doesn't exist.</returns>
        public SmartPointerType GetSmartPointerType() =>
            Visualizer.Items?.OfType<SmartPointerType>().FirstOrDefault();

        /// <summary>
        /// Recursively matches type wildcards (i.e. "*" in NatVis).
        /// </summary>
        /// <param name="vizType">NatVis type specification (might contain wildcards).</param>
        /// <param name="exactType">Exact type matched.</param>
        /// <param name="resolvedWildcards">
        /// List were types that replace wildcards in vizType are added to.
        /// </param>
        private void ResolveWildcards(TypeName vizType, TypeName exactType,
                                      List<string> resolvedWildcards)
        {
            Debug.Assert(!vizType.IsWildcard, "The top-level type should not be a wildcard");

            // At this point, the `exactType` should be matched to the Natvis visualizer `vizType`.
            // That means that it holds `vizType.Args.Count <= exactType.Args.Count`. In the case
            // of inequality, the last argument in `vizType.Args` should be a wildcard, which
            // applies to all the remaining arguments of `exactType`.
            Debug.Assert(vizType.Args.Count <= exactType.Args.Count, "Invalid type match");
            Debug.Assert(
                vizType.Args.Count == exactType.Args.Count ||
                    (vizType.Args.Count > 0 && vizType.Args[vizType.Args.Count - 1].IsWildcard),
                "Invalid type match");

            for (int i = 0; i < vizType.Args.Count; ++i)
            {
                if (vizType.Args[i].IsWildcard)
                {
                    resolvedWildcards.Add(exactType.Args[i].FullyQualifiedName);
                }
                else
                {
                    ResolveWildcards(vizType.Args[i], exactType.Args[i], resolvedWildcards);
                }
            }

            // Add any remaining elements of `exactType`.
            for (int i = vizType.Args.Count; i < exactType.Args.Count; ++i)
            {
                resolvedWildcards.Add(exactType.Args[i].FullyQualifiedName);
            }
        }
    }
}
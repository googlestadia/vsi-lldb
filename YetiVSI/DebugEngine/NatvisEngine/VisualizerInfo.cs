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
using System.Linq;

namespace YetiVSI.DebugEngine.NatvisEngine
{
    public class VisualizerInfo
    {
        public VisualizerType Visualizer { get; }
        public NatvisScope NatvisScope { get; }

        public VisualizerInfo(VisualizerType viz, TypeName name)
        {
            Visualizer = viz;
            // add the template parameter macro values
            NatvisScope = new NatvisScope();
            for (int i = 0; i < name.Args.Count; ++i)
            {
                NatvisScope.AddScopedName($"$T{i + 1}", name.Args[i].FullyQualifiedName);
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
    }
}
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

﻿// This file is defined in the YetiCommon assembly as a workaround while it needs to be referenced
// by both YetiVSI and VSFake. It should be moved back to YetiVSI when possible (see (internal)).
using System.Collections.Generic;

namespace YetiCommon.VSProject
{
    /// <summary>
    /// Interacts with a Visual Studio solution and produces a list of all projects in it.
    /// </summary>
    public interface ISolutionExplorer
    {
        /// <summary>
        /// List all projects currently in the solution.
        /// </summary>
        IEnumerable<ISolutionExplorerProject> EnumerateProjects();
    }

    /// <summary>
    /// Contains properties of a Visual Studio project.
    /// </summary>
    public interface ISolutionExplorerProject
    {
        /// <summary>
        /// User-specified project output directory.
        /// </summary>
        string OutputDirectory { get; }

        /// <summary>
        /// Full local path to the target executable's directory.
        /// </summary>
        string TargetDirectory { get; }
    }
}

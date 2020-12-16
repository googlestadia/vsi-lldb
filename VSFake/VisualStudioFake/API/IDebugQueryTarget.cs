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

﻿using Microsoft.VisualStudio.ProjectSystem.Debug;
using Microsoft.VisualStudio.ProjectSystem.VS.Debug;
using System.Collections.Generic;
using YetiCommon.VSProject;

namespace Google.VisualStudioFake.API
{
    /// <summary>
    /// Based on Microsoft.VisualStudio.ProjectSystem.VS.Debuggers.IDebugQueryTarget.
    /// </summary>
    public interface IDebugQueryTarget
    {
        /// <summary>
        /// Gets the debugger initialization settings that would be used to launch the debugger.
        /// </summary>
        /// <param name="project">Project being debugged.</param>
        /// <param name="launchOptions">
        /// The flags that would be passed to
        /// Microsoft.VisualStudio.ProjectSystem.Debuggers.IDebugLaunchProvider.LaunchAsync
        /// to actually launch the debugger.
        /// </param>
        /// <returns>
        /// An array of elements that each describe a debuggee process to launch.
        /// </returns>
        /// <remarks>
        /// The list returned must have at least one element.
        /// </remarks>
        IReadOnlyList<IDebugLaunchSettings> QueryDebugTargets(
            IAsyncProject project, DebugLaunchOptions launchOptions);
    }
}

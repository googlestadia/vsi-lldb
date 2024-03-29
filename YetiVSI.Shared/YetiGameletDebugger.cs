﻿// Copyright 2020 Google LLC
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

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Debug;
using Microsoft.VisualStudio.ProjectSystem.VS.Debug;
using Microsoft.VisualStudio.Shell;
using YetiVSI.ProjectSystem.Abstractions;

namespace YetiVSI
{
    // This class implements the YetiGameletDebugger debug launcher.
    [ExportDebugger("YetiGameletDebugger")]
    [AppliesTo(ProjectCapabilities.VisualC)]
    public class YetiGameletDebugger : GameletDebugger
    {
        [ImportingConstructor]
        public YetiGameletDebugger(ConfiguredProject configuredProject)
            : base(configuredProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
        }
    }

    // This class implements the GgpGameletDebugger debug launcher.
    [ExportDebugger("GgpGameletDebugger")]
    [AppliesTo(ProjectCapabilities.VisualC)]
    public class GgpGameletDebugger : GameletDebugger
    {
        [ImportingConstructor]
        public GgpGameletDebugger(ConfiguredProject configuredProject)
            : base(configuredProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
        }
    }

    public class GameletDebugger : DebugLaunchProviderBase
    {
        readonly GgpDebugQueryTarget ggpDebugQueryTarget;
        readonly IAsyncProject project;

        public GameletDebugger(ConfiguredProject configuredProject)
            : base(configuredProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var serviceManager = new ServiceManager();
            var dialogUtil = new DialogUtil();
            var compRoot = new GgpDebugQueryTargetCompRoot(serviceManager, dialogUtil);
            ggpDebugQueryTarget = compRoot.Create();
            project = new ConfiguredProjectAdapter(configuredProject);
        }

        public override Task<bool> CanLaunchAsync(DebugLaunchOptions launchOptions)
            => System.Threading.Tasks.Task.FromResult(true);

        public override Task<IReadOnlyList<IDebugLaunchSettings>> QueryDebugTargetsAsync(
            DebugLaunchOptions launchOptions)
                => ggpDebugQueryTarget.QueryDebugTargetsAsync(project, launchOptions);
    }
}

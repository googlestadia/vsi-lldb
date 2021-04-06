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

﻿using Google.VisualStudioFake.API;
using Google.VisualStudioFake.Internal.Interop;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.ProjectSystem.Debug;
using Microsoft.VisualStudio.ProjectSystem.VS.Debug;
using System.Collections.Generic;
using YetiVSI.ProjectSystem.Abstractions;

namespace Google.VisualStudioFake.Internal
{
    public class TargetAdapter : ITargetAdapter
    {
        readonly API.IDebugQueryTarget debugQueryTarget;

        readonly DebugPortNotify.Factory portNotifyFactory;
        readonly DefaultPort.Factory defaultPortFactory;
        readonly Process.Factory processFactory;

        public TargetAdapter(API.IDebugQueryTarget debugQueryTarget,
            DebugPortNotify.Factory portNotifyFactory, DefaultPort.Factory defaultPortFactory,
            Process.Factory processFactory)
        {
            this.debugQueryTarget = debugQueryTarget;
            this.portNotifyFactory = portNotifyFactory;
            this.defaultPortFactory = defaultPortFactory;
            this.processFactory = processFactory;
        }

        public IDebugPort2 InitializePort(IDebugEventCallback2 callback, IDebugEngine2 debugEngine)
        {
            // When attaching to a core, the port and process come from our implementation of a
            // port supplier. if we are launching a new game, where does it come from?
            // I think this is a default port that handles all windows based processes.
            // https://docs.microsoft.com/en-us/visualstudio/extensibility/debugger/ports?view=vs-2017
            var portNotify = portNotifyFactory.Create(callback, debugEngine);
            var port = defaultPortFactory.Create(portNotify);
            var process = processFactory.Create("Default Process", port);
            port.SetProcess(process);
            return port;
        }

        public IReadOnlyList<IDebugLaunchSettings> QueryDebugTargets(
            IAsyncProject project, DebugLaunchOptions launchOptions) =>
            debugQueryTarget.QueryDebugTargets(project, launchOptions);
    }
}

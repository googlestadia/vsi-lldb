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
using YetiVSI.ProjectSystem.Abstractions;

namespace Google.VisualStudioFake.Internal
{
    public class VSFake : IVSFake
    {
        public VSFake(ITargetAdapter targetAdapter, IProjectAdapter projectAdapter,
                      ISessionDebugManager sessionDebugManager, ISolutionExplorer solutionExplorer,
                      IDebugSession debugSession, VSFakeTimeoutSource timeouts)
        {
            TargetAdapter = targetAdapter;
            ProjectAdapter = projectAdapter;
            SessionDebugManager = sessionDebugManager;
            SolutionExplorer = solutionExplorer;
            DebugSession = debugSession;
            Timeouts = timeouts;
        }

        #region IVSFake

        public IDebugSession DebugSession { get; }

        public IProjectAdapter ProjectAdapter { get; }

        public ISessionDebugManager SessionDebugManager { get; }

        public ITargetAdapter TargetAdapter { get; }

        public ISolutionExplorer SolutionExplorer { get; }

        public VSFakeTimeoutSource Timeouts { get; }

        #endregion
    }
}

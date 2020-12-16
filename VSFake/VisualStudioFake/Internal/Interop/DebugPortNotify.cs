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

﻿using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System.Collections.Generic;

namespace Google.VisualStudioFake.Internal.Interop
{
    public class DebugPortNotify : IDebugPortNotify2
    {
        public class Factory
        {
            readonly DebugProgram.Factory debugProgramFactory;
            readonly DefaultPort.Factory defaultPortFactory;
            readonly Process.Factory processFactory;

            public Factory(DebugProgram.Factory debugProgramFactory,
                DefaultPort.Factory defaultPortFactory, Process.Factory processFactory)
            {
                this.debugProgramFactory = debugProgramFactory;
                this.defaultPortFactory = defaultPortFactory;
                this.processFactory = processFactory;
            }

            public IDebugPortNotify2 Create(
                IDebugEventCallback2 callback, IDebugEngine2 debugEngine) => new DebugPortNotify(
                    callback, debugEngine, debugProgramFactory, defaultPortFactory, processFactory);
        }

        readonly List<IDebugProgramNode2> programNodes;
        readonly IDebugEventCallback2 callback;
        readonly IDebugEngine2 debugEngine;

        readonly DebugProgram.Factory debugProgramFactory;
        readonly DefaultPort.Factory defaultPortFactory;
        readonly Process.Factory processFactory;

        DebugPortNotify(IDebugEventCallback2 callback, IDebugEngine2 debugEngine,
            DebugProgram.Factory debugProgramFactory, DefaultPort.Factory defaultPortFactory,
            Process.Factory processFactory)
        {
            programNodes = new List<IDebugProgramNode2>();

            this.callback = callback;
            this.debugEngine = debugEngine;
            this.debugProgramFactory = debugProgramFactory;
            this.defaultPortFactory = defaultPortFactory;
            this.processFactory = processFactory;
        }

        public int AddProgramNode(IDebugProgramNode2 programNode)
        {
            programNodes.Add(programNode);
            var port = defaultPortFactory.Create(this);
            // TODO: Should this synchronously call IDebugEngine2.Attach() or should it
            // add a job to the job queue?
            var result = debugEngine.Attach(
                new IDebugProgram2[]
                {
                    debugProgramFactory.Create(processFactory.Create("Game Process", port))
                },
                new IDebugProgramNode2[] { programNode }, 1, callback,
                enum_ATTACH_REASON.ATTACH_REASON_LAUNCH);
            HResultChecker.Check(result);
            return VSConstants.S_OK;
        }

        public int RemoveProgramNode(IDebugProgramNode2 pProgramNode)
        {
            if (programNodes.Remove(pProgramNode))
            {
                return VSConstants.S_OK;
            }
            return VSConstants.S_FALSE;
        }
    }
}

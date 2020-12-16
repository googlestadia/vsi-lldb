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

using DebuggerApi;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using YetiCommon.CastleAspects;
using YetiVSI.DebugEngine.Interfaces;
using YetiVSI.Util;

namespace YetiVSI.DebugEngine
{
    public class LldbBreakpointManager : SimpleDecoratorSelf<IBreakpointManager>, IBreakpointManager
    {
        public class Factory
        {
            private readonly JoinableTaskContext taskContext;
            private readonly DebugPendingBreakpoint.Factory pendingBreakpointFactory;
            private readonly DebugWatchpoint.Factory watchpointFactory;

            [Obsolete("This constructor only exists to support mocking libraries.", error: true)]
            protected Factory() { }

            public Factory(JoinableTaskContext taskContext,
                DebugPendingBreakpoint.Factory pendingBreakpointFactory,
                DebugWatchpoint.Factory watchpointFactory)
            {
                this.taskContext = taskContext;
                this.pendingBreakpointFactory = pendingBreakpointFactory;
                this.watchpointFactory = watchpointFactory;
            }

            public virtual IBreakpointManager Create(IDebugEngineHandler debugEngineHandler,
                IGgpDebugProgram debugProgram)
            {
                return new LldbBreakpointManager(taskContext, pendingBreakpointFactory,
                    watchpointFactory, debugEngineHandler, debugProgram);
            }
        }

        JoinableTaskContext taskContext;
        IDebugEngineHandler debugEngineHandler;
        IGgpDebugProgram debugProgram;
        DebugPendingBreakpoint.Factory pendingBreakpointFactory;
        DebugWatchpoint.Factory watchpointFactory;
        Dictionary<int, IPendingBreakpoint> pendingBreakpoints =
            new Dictionary<int, IPendingBreakpoint>();
        Dictionary<int, IWatchpoint> watchpoints =
            new Dictionary<int, IWatchpoint>();
        Dictionary<int, int> watchpointsCount = new Dictionary<int, int>();

        private LldbBreakpointManager(
            JoinableTaskContext taskContext,
            DebugPendingBreakpoint.Factory pendingBreakpointFactory,
            DebugWatchpoint.Factory watchpointFactory,
            IDebugEngineHandler debugEngineHandler,
            IGgpDebugProgram debugProgram)
        {
            this.taskContext = taskContext;
            this.pendingBreakpointFactory = pendingBreakpointFactory;
            this.watchpointFactory = watchpointFactory;
            this.debugEngineHandler = debugEngineHandler;
            this.debugProgram = debugProgram;
        }

        public void CreatePendingBreakpoint(IDebugBreakpointRequest2 breakpointRequest,
            RemoteTarget target, out IDebugPendingBreakpoint2 pendingBreakpoint)
        {
            taskContext.ThrowIfNotOnMainThread();

            BP_REQUEST_INFO[] requestInfo = new BP_REQUEST_INFO[1];
            breakpointRequest.GetRequestInfo(enum_BPREQI_FIELDS.BPREQI_BPLOCATION, requestInfo);
            if (requestInfo[0].bpLocation.bpLocationType ==
                (uint)enum_BP_LOCATION_TYPE.BPLT_DATA_STRING)
            {
                pendingBreakpoint = watchpointFactory.Create(Self,
                    breakpointRequest, target, debugProgram);
            }
            else
            {
                pendingBreakpoint = pendingBreakpointFactory.Create(
                    Self, debugProgram, breakpointRequest, target);
            }
        }

        public void RegisterPendingBreakpoint(IPendingBreakpoint pendingBreakpoint)
        {
            int id = pendingBreakpoint.GetId();
            if (id != -1)
            {
                pendingBreakpoints[id] = pendingBreakpoint;
            }
            else
            {
                Trace.WriteLine(
                    "Failed to register PendingBreakpoint: breakpoint does not have an ID.");
            }
            debugEngineHandler.OnBreakpointBound(pendingBreakpoint, debugProgram);
        }

        public void RegisterWatchpoint(IWatchpoint watchpoint)
        {
            int id = watchpoint.GetId();
            if (id != -1)
            {
                watchpoints[id] = watchpoint;

                int currentCount;
                watchpointsCount.TryGetValue(id, out currentCount);
                watchpointsCount[id] = currentCount + 1;
            }
            else
            {
                Trace.WriteLine(
                    "Failed to register pending watchpoint: watchpoint does not have an ID.");
            }
            debugEngineHandler.OnWatchpointBound(watchpoint, debugProgram);
        }

        public void UnregisterWatchpoint(IWatchpoint watchpoint)
        {
            int id = watchpoint.GetId();
            if (id != -1)
            {
                int currentCount;
                watchpointsCount.TryGetValue(id, out currentCount);
                if (currentCount > 1)
                {
                    watchpointsCount[id] = currentCount - 1;
                }
                else if (currentCount == 1)
                {
                    watchpointsCount.Remove(id);
                    watchpoints.Remove(id);
                }
            }
            else
            {
                Trace.WriteLine(
                    "Failed to unregister pending watchpoint: watchpoint does not have an ID.");
            }
        }

        public int GetWatchpointRefCount(IWatchpoint watchpoint)
        {
            int currentCount = 0;
            int id = watchpoint.GetId();
            if (id != -1)
            {
                watchpointsCount.TryGetValue(id, out currentCount);
            }
            return currentCount;
        }

        public void ReportBreakpointError(DebugBreakpointError error)
            => debugEngineHandler.OnBreakpointError(error, debugProgram);

        public bool GetPendingBreakpointById(int id, out IPendingBreakpoint pendingBreakpoint)
        {
            return pendingBreakpoints.TryGetValue(id, out pendingBreakpoint);
        }

        public bool GetWatchpointById(int id, out IWatchpoint watchpoint)
        {
            return watchpoints.TryGetValue(id, out watchpoint);
        }

        public uint GetNumPendingBreakpoints()
        {
            return (uint)pendingBreakpoints.Count;
        }

        public uint GetNumBoundBreakpoints()
        {
            return (uint)pendingBreakpoints.Values.Sum(bp => bp.GetNumLocations());
        }
    }
}

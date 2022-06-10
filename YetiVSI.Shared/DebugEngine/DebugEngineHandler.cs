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
using DebuggerApi;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio;
using System.Diagnostics;
using YetiVSI.DebugEngine.Exit;
using Microsoft.VisualStudio.Threading;
using System.Threading.Tasks;

namespace YetiVSI.DebugEngine
{
    /// <summary>
    /// The debug engine handler is used to send debug events to the SDM.
    /// </summary>
    public interface IDebugEngineHandler
    {
        /// <summary>
        /// Converts a RemoteThread into an IDebugThread2, then sends a DebugEvent associated with
        /// the IDebugThread2 to the Visual Studio SDM.
        /// </summary>
        int SendEvent(IGgpDebugEvent evnt, IGgpDebugProgram program, RemoteThread thread);

        /// <summary>
        /// Called to send a DebugEvent associated with the IDebugThread2 to the Visual Studio SDM.
        /// </summary>
        int SendEvent(IGgpDebugEvent evnt, IGgpDebugProgram program, IDebugThread2 thread);
    }

    public static class DebugEngineHandlerExtensions
    {
        /// <summary>
        /// Called to send a DebugEvent with no associated thread to the Visual Studio SDM.
        /// </summary>
        public static int SendEvent(this IDebugEngineHandler handler, IGgpDebugEvent evnt,
                                    IGgpDebugProgram program) =>
            handler.SendEvent(evnt, program, (IDebugThread2)null);

        /// <summary>
        /// Called to abort the debug engine.
        /// </summary>
        public static void Abort(this IDebugEngineHandler handler, IGgpDebugProgram program,
                                 ExitInfo exitInfo) =>
            handler.SendEvent(new ProgramDestroyEvent(exitInfo), program);

        /// <summary>
        /// Send a breakpoint error event to the SDM.
        /// </summary>
        public static void OnBreakpointError(this IDebugEngineHandler handler,
                                             DebugBreakpointError breakpointError,
                                             IGgpDebugProgram program) =>
            handler.SendEvent(new BreakpointErrorEvent(breakpointError), program);

        /// <summary>
        /// Send a breakpoint bound event to the SDM.
        /// </summary>
        public static void OnBreakpointBound(
            this IDebugEngineHandler handler, IPendingBreakpoint pendingBreakpoint,
            IEnumerable<IDebugBoundBreakpoint2> newlyBoundBreakpoints,
            BoundBreakpointEnumFactory breakpointBoundEnumFactory,
            IGgpDebugProgram program) =>
            handler.SendEvent(
                new BreakpointBoundEvent(pendingBreakpoint, newlyBoundBreakpoints,
                                         breakpointBoundEnumFactory), program);

        /// <summary>
        /// Send a watchpoint bound event to the SDM.
        /// </summary>
        public static void OnWatchpointBound(this IDebugEngineHandler handler,
                                             IWatchpoint pendingWatchpoint,
                                             IGgpDebugProgram program) =>
            handler.SendEvent(new BreakpointBoundEvent(pendingWatchpoint), program);

        /// <summary>
        /// Send a module load or unload event to the SDM.
        /// </summary>
        public static void OnModuleLoad(this IDebugEngineHandler handler, IDebugModule2 module,
                                        IGgpDebugProgram program) =>
            handler.SendEvent(new DebugModuleLoadEvent(module, true), program);

        /// <summary>
        /// Send a symbols loaded event.
        /// </summary>
        public static void OnSymbolsLoaded(this IDebugEngineHandler handler, IDebugModule3 module,
                                           string moduleName, string errorMessage, bool loaded,
                                           IGgpDebugProgram program) =>
            handler.SendEvent(new DebugSymbolSearchEvent(module, moduleName, errorMessage, loaded),
                              program);

        public static int OnEvaluationComplete(this IDebugEngineHandler handler,
                                               IDebugExpression2 expr, IDebugProperty2 result,
                                               IGgpDebugProgram program, IDebugThread2 thread) =>
            handler.SendEvent(new DebugExpressionEvaluationCompleteEvent(expr, result), program,
                              thread);

        /// <summary>
        /// Send a module unload event to the SDM.
        /// </summary>
        public static void OnModuleUnload(this IDebugEngineHandler handler, IDebugModule2 module,
                                          IGgpDebugProgram program) =>
            handler.SendEvent(new DebugModuleLoadEvent(module, false), program);
    }

    public interface IDebugEngineHandlerFactory
    {
        IDebugEngineHandler Create(IDebugEngine2 debugEngine, IDebugEventCallback2 eventCallback);
    }

    public class DebugEngineHandler : IDebugEngineHandler
    {
        public class Factory : IDebugEngineHandlerFactory
        {
            readonly JoinableTaskContext _taskContext;

            public Factory(JoinableTaskContext taskContext)
            {
                _taskContext = taskContext;
            }

            public IDebugEngineHandler Create(IDebugEngine2 debugEngine,
                                              IDebugEventCallback2 eventCallback) =>
                new DebugEngineHandler(_taskContext, debugEngine, eventCallback);
        }

        readonly JoinableTaskContext _taskContext;
        readonly IDebugEngine2 _debugEngine;
        readonly IDebugEventCallback2 _eventCallback;

        public DebugEngineHandler(JoinableTaskContext taskContext, IDebugEngine2 debugEngine,
                                  IDebugEventCallback2 eventCallback)
        {
            _taskContext = taskContext;
            _debugEngine = debugEngine;
            _eventCallback = eventCallback;
        }

        // Uses the callback to send a debug event to the SDM.
        // The callback is provided to us when the SDM calls LaunchSuspended.
        // Certain events require, allow, or do not allow program or thread objects
        // to be sent with them. Reference:
        // https://docs.microsoft.com/en-us/visualstudio/extensibility/debugger/supported-event-types
        public int SendEvent(IGgpDebugEvent evnt, IGgpDebugProgram program, IDebugThread2 thread)
        {
            return _taskContext.Factory.Run(async () =>
            {
                return await SendEventAsync(evnt, program, thread);
            });
        }

        public int SendEvent(IGgpDebugEvent evnt, IGgpDebugProgram program,
                             RemoteThread thread) => SendEvent(evnt, program,
                                                               program.GetDebugThread(thread));

        async Task<int> SendEventAsync(IGgpDebugEvent evnt, IGgpDebugProgram program,
                                       IDebugThread2 thread)
        {
            await _taskContext.Factory.SwitchToMainThreadAsync();

            if (((IDebugEvent2)evnt).GetAttributes(out uint attributes) != VSConstants.S_OK)
            {
                Trace.WriteLine($"Could not get event attributes of event ({evnt})");
                return VSConstants.E_FAIL;
            }

            Guid eventId = evnt.EventId;
            return _eventCallback.Event(_debugEngine, null, program, thread, evnt, ref eventId,
                                        attributes);
        }
    }
}
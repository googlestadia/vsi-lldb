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
using System.Linq;
using System.Threading.Tasks;
using YetiVSI.DebugEngine.AsyncOperations;
using YetiVSI.DebugEngine.Exit;
using YetiVSI.DebugEngine.Interfaces;
using YetiVSI.LLDBShell;
using YetiVSI.Util;

namespace YetiVSI.DebugEngine
{
    /// <summary>
    /// LldbAttachedProgram represents a program in the attached state.
    /// </summary>
    public interface ILldbAttachedProgram
    {
        /// <summary>
        /// Start the debugger using the given debug engine. This sends events to the SDM which
        /// tells it that we are ready to debug.
        /// </summary>
        void Start(IDebugEngine2 debugEngine);

        /// <summary>
        /// Stop listening for debug events from the lldb server.
        /// </summary>
        void Stop();

        /// <summary>
        /// Abort tells the SDM to stop debugging.
        /// </summary>
        void Abort(ExitInfo exitInfo);

        /// <summary>
        /// Start listening for events from lldb and tell the running process to continue.
        /// </summary>
        void ContinueFromSuspended();

        /// <summary>
        /// Send an exception event to the SDM to trigger breakmode.
        /// </summary>
        void ContinueInBreakMode();

        /// <summary>
        /// Create a breakpoint. This notifies LLDB of the breakpoint location.
        /// </summary>
        IDebugPendingBreakpoint2 CreatePendingBreakpoint(
            IDebugBreakpointRequest2 breakpointRequest);

        /// <summary>
        /// Getter for number of pending breakpoints.
        /// </summary>
        uint GetNumPendingBreakpoints();

        /// <summary>
        /// Getter for number of bound breakpoints.
        /// </summary>
        uint GetNumBoundBreakpoints();

        /// <summary>
        /// Get the number of loaded modules from the LLDB target.
        /// </summary>
        int NumLoadedModules { get; }

        /// <summary>
        /// Load the symbols. Skips modules that already have their symbols loaded.
        /// </summary>
        Task<int> LoadModuleFilesAsync(SymbolInclusionSettings symbolsSettings,
                                       bool useSymbolsStores, ICancelable task,
                                       IModuleFileLoadMetricsRecorder moduleFileLoadRecorder);

        /// <summary>
        /// Get the modules with the specified name.
        /// </summary>
        /// <param name="moduleName">Name of the module to look for.</param>
        /// <returns>IList of modules with the specified name.</returns>
        IList<IDebugModule3> GetModulesByName(string moduleName);

        /// <summary>
        /// Sets how exceptions should be handled.
        /// </summary>
        /// <param name="exceptions"></param>
        void SetExceptions(IEnumerable<EXCEPTION_INFO> exceptions);

        /// <summary>
        /// Returns the process id of the remote process being debugged.
        /// </summary>
        uint RemotePid { get; }
    }

    public interface ILldbAttachedProgramFactory
    {
        ILldbAttachedProgram Create(
            IDebugProcess2 debugProcess, Guid programId, IDebugEngine2 debugEngine,
            IDebugEventCallback2 callback, SbDebugger debugger, RemoteTarget target,
            LldbListenerSubscriber listenerSubscriber, SbProcess process,
            SbCommandInterpreter commandInterpreter, bool isCoreAttach,
            IExceptionManager exceptionManager, IModuleSearchLogHolder moduleSearchLogHolder,
            uint remotePid);
    }

    public class LldbAttachedProgram : ILldbAttachedProgram
    {
        public class Factory : ILldbAttachedProgramFactory
        {
            readonly JoinableTaskContext _taskContext;
            readonly IDebugEngineHandlerFactory _debugEngineHandlerFactory;
            readonly ITaskExecutor _taskExecutor;
            readonly IDebugProgramFactory _debugProgramFactory;
            readonly CreateDebugThreadDelegate _debugThreadCreatorDelegate;
            readonly DebugModule.Factory _debugModuleFactory;
            readonly DebugModuleCache.Factory _debugModuleCacheFactory;
            readonly CreateDebugStackFrameDelegate _debugStackFrameCreator;
            readonly LldbEventManager.Factory _eventManagerFactory;
            readonly ILLDBShell _lldbShell;
            readonly LldbBreakpointManager.Factory _breakpointManagerFactory;
            readonly SymbolLoader.Factory _symbolLoaderFactory;
            readonly BinaryLoader.Factory _binaryLoaderFactory;
            readonly IModuleFileLoaderFactory _moduleFileLoaderFactory;

            public Factory(JoinableTaskContext taskContext,
                           IDebugEngineHandlerFactory debugEngineHandlerFactory,
                           ITaskExecutor taskExecutor, LldbEventManager.Factory eventManagerFactory,
                           IDebugProgramFactory debugProgramFactory,
                           DebugModuleCache.Factory debugModuleCacheFactory,
                           DebugModule.Factory debugModuleFactory,
                           CreateDebugThreadDelegate debugThreadCreatorDelegate,
                           CreateDebugStackFrameDelegate debugStackFrameCreator,
                           ILLDBShell lldbShell,
                           LldbBreakpointManager.Factory breakpointManagerFactory,
                           SymbolLoader.Factory symbolLoaderFactory,
                           BinaryLoader.Factory binaryLoaderFactory,
                           IModuleFileLoaderFactory moduleFileLoaderFactory)
            {
                _taskContext = taskContext;
                _debugEngineHandlerFactory = debugEngineHandlerFactory;
                _taskExecutor = taskExecutor;
                _eventManagerFactory = eventManagerFactory;
                _debugProgramFactory = debugProgramFactory;
                _debugModuleCacheFactory = debugModuleCacheFactory;
                _debugModuleFactory = debugModuleFactory;
                _debugThreadCreatorDelegate = debugThreadCreatorDelegate;
                _debugStackFrameCreator = debugStackFrameCreator;
                _breakpointManagerFactory = breakpointManagerFactory;
                _lldbShell = lldbShell;
                _symbolLoaderFactory = symbolLoaderFactory;
                _binaryLoaderFactory = binaryLoaderFactory;
                _moduleFileLoaderFactory = moduleFileLoaderFactory;
            }

            public ILldbAttachedProgram Create(
                IDebugProcess2 debugProcess, Guid programId, IDebugEngine2 debugEngine,
                IDebugEventCallback2 callback, SbDebugger debugger, RemoteTarget target,
                LldbListenerSubscriber listenerSubscriber, SbProcess process,
                SbCommandInterpreter commandInterpreter, bool isCoreAttach,
                IExceptionManager exceptionManager, IModuleSearchLogHolder moduleSearchLogHolder,
                uint remotePid)
            {
                // Required due to an issue triggered by the proxy used to wrap debugProgramFactory.
                // TODO: Remove assertion once the issue with Castle.DynamicProxy is
                // fixed.
                _taskContext.ThrowIfNotOnMainThread();

                var debugEngineHandler = _debugEngineHandlerFactory.Create(debugEngine, callback);

                var binaryLoader = _binaryLoaderFactory.Create(target);
                var symbolLoader = _symbolLoaderFactory.Create(commandInterpreter);
                var moduleFileLoader = _moduleFileLoaderFactory.Create(symbolLoader, binaryLoader,
                                                                       moduleSearchLogHolder);

                var debugModuleCache = _debugModuleCacheFactory.Create(
                    (lldbModule, loadOrder, ggpProgram) => _debugModuleFactory.Create(
                        moduleFileLoader, moduleSearchLogHolder, lldbModule, loadOrder,
                        debugEngineHandler, ggpProgram));
                var ad7FrameInfoCreator = new AD7FrameInfoCreator(debugModuleCache);

                var stackFrameCreator = new StackFramesProvider.StackFrameCreator(
                    (frame, thread, program) => _debugStackFrameCreator(
                        ad7FrameInfoCreator, frame, thread, debugEngineHandler, program));
                var threadCreator = new DebugProgram.ThreadCreator(
                    (thread, program) => _debugThreadCreatorDelegate(
                        ad7FrameInfoCreator, stackFrameCreator, thread, program));
                var debugProgram = _debugProgramFactory.Create(
                    debugEngineHandler, threadCreator, debugProcess, programId, process, target,
                    debugModuleCache, isCoreAttach);

                _taskExecutor.StartAsyncTasks(
                    ex => debugEngineHandler.Abort(debugProgram, ExitInfo.Error(ex)));

                var breakpointManager =
                    _breakpointManagerFactory.Create(debugEngineHandler, debugProgram);
                var eventManager =
                    _eventManagerFactory.Create(debugEngineHandler, breakpointManager, debugProgram,
                                                process, listenerSubscriber);

                // TODO: Listen for module load/unload events from LLDB
                binaryLoader.LldbModuleReplaced += (o, args) =>
                {
                    debugModuleCache.GetOrCreate(args.AddedModule, debugProgram);
                    debugModuleCache.Remove(args.RemovedModule);
                };
                debugModuleCache.ModuleAdded += (o, args) =>
                    debugEngineHandler.OnModuleLoad(args.Module, debugProgram);
                debugModuleCache.ModuleRemoved += (o, args) =>
                    debugEngineHandler.OnModuleUnload(args.Module, debugProgram);

                return new LldbAttachedProgram(breakpointManager, eventManager, _lldbShell,
                                               moduleFileLoader, debugEngineHandler, _taskExecutor,
                                               debugProgram, debugger, target, process,
                                               exceptionManager, debugModuleCache,
                                               listenerSubscriber, remotePid);
            }
        }

        readonly IEventManager _eventManager;
        readonly ILLDBShell _lldbShell;
        readonly IBreakpointManager _breakpointManager;
        readonly IModuleFileLoader _moduleFileLoader;
        readonly IExceptionManager _exceptionManager;

        readonly IDebugEngineHandler _debugEngineHandler;
        readonly ITaskExecutor _taskExecutor;

        readonly IGgpDebugProgram _debugProgram;
        readonly SbDebugger _debugger;
        readonly RemoteTarget _target;
        readonly SbProcess _process;

        readonly IDebugModuleCache _debugModuleCache;
        readonly ILldbListenerSubscriber _listenerSubscriber;

        public LldbAttachedProgram(IBreakpointManager breakpointManager, IEventManager eventManager,
                                   ILLDBShell lldbShell, IModuleFileLoader moduleFileLoader,
                                   IDebugEngineHandler debugEngineHandler,
                                   ITaskExecutor taskExecutor, IGgpDebugProgram debugProgram,
                                   SbDebugger debugger, RemoteTarget target, SbProcess process,
                                   IExceptionManager exceptionManager,
                                   IDebugModuleCache debugModuleCache,
                                   ILldbListenerSubscriber listenerSubscriber, uint remotePid)
        {
            _debugProgram = debugProgram;
            _breakpointManager = breakpointManager;
            _eventManager = eventManager;
            _lldbShell = lldbShell;
            _moduleFileLoader = moduleFileLoader;
            _debugEngineHandler = debugEngineHandler;
            _taskExecutor = taskExecutor;
            _debugger = debugger;
            _target = target;
            _process = process;
            _exceptionManager = exceptionManager;
            _debugModuleCache = debugModuleCache;
            _listenerSubscriber = listenerSubscriber;
            RemotePid = remotePid;
        }

        /// <summary>
        /// Start the debugger using the given debug engine. This sends events to the SDM which
        /// tells it that we are ready to debug.
        /// </summary>
        public void Start(IDebugEngine2 debugEngine)
        {
            _listenerSubscriber.BreakpointChanged += OnBreakpointChanged;
            // The order of these two events is important!! Visual studio always needs to know that
            // the engine has been created before the program is created.
            _debugEngineHandler.SendEvent(new EngineCreateEvent(debugEngine), _debugProgram);
            _debugEngineHandler.SendEvent(new ProgramCreateEvent(), _debugProgram);
        }

        /// <summary>
        /// Stop listening for debug events from the lldb server.
        /// </summary>
        public void Stop()
        {
            _eventManager.StopListener();
            _listenerSubscriber.BreakpointChanged -= OnBreakpointChanged;
            _taskExecutor.AbortAsyncTasks();
            _lldbShell.RemoveDebugger(_debugger);
        }

        /// <summary>
        /// Abort tells the SDM to stop debugging.
        /// </summary>
        public void Abort(ExitInfo exitInfo) => _debugEngineHandler.Abort(_debugProgram, exitInfo);

        /// <summary>
        /// Start listening for events from lldb and tell the running process to continue.
        /// </summary>
        public void ContinueFromSuspended()
        {
            _eventManager.StartListener();
            _process.Continue();
            _lldbShell.AddDebugger(_debugger);
            UpdateModulesList();
        }

        /// <summary>
        /// Send an exception event to the SDM to trigger breakmode.
        /// </summary>
        public void ContinueInBreakMode()
        {
            RemoteThread thread = _process.GetSelectedThread();
            ExceptionEvent exceptionEvent;
            if (thread.GetStopReason() == StopReason.SIGNAL && thread.GetStopReasonDataCount() > 0)
            {
                var signalNumber = thread.GetStopReasonDataAtIndex(0);
                (string name, string description) = SignalMap.Map[signalNumber];
                enum_EXCEPTION_STATE exceptionState =
                    enum_EXCEPTION_STATE.EXCEPTION_STOP_ALL |
                    enum_EXCEPTION_STATE.EXCEPTION_CANNOT_BE_CONTINUED;
                exceptionEvent =
                    new ExceptionEvent(name, (uint)signalNumber, exceptionState, description);
            }
            else
            {
                exceptionEvent = new ExceptionEvent("", 0, enum_EXCEPTION_STATE.EXCEPTION_NONE, "");
            }

            _debugEngineHandler.SendEvent(exceptionEvent, _debugProgram, thread);
            _lldbShell.AddDebugger(_debugger);
            UpdateModulesList();
        }

        /// <summary>
        /// Create a breakpoint. This notifies LLDB of the breakpoint location.
        /// </summary>
        public IDebugPendingBreakpoint2 CreatePendingBreakpoint(
            IDebugBreakpointRequest2 breakpointRequest)
        {
            _breakpointManager.CreatePendingBreakpoint(breakpointRequest, _target,
                                                       out IDebugPendingBreakpoint2
                                                           pendingBreakpoint);
            return pendingBreakpoint;
        }

        public uint GetNumPendingBreakpoints() => _breakpointManager.GetNumPendingBreakpoints();

        public uint GetNumBoundBreakpoints() => _breakpointManager.GetNumBoundBreakpoints();

        /// <summary>
        /// Get the number of loaded modules from the LLDB target.
        /// </summary>
        public int NumLoadedModules => _target.GetNumModules();

        /// <summary>
        /// Load missing binaries and symbols. Skips modules that already have their symbols loaded
        /// or that are excluded via Include / Exclude options on symbols settings page.
        /// </summary>
        public Task<int> LoadModuleFilesAsync(
            SymbolInclusionSettings symbolsSettings, bool useSymbolStores, ICancelable task,
            IModuleFileLoadMetricsRecorder moduleFileLoadRecorder)
        {
            return _moduleFileLoader.LoadModuleFilesAsync(
                Enumerable.Range(0, NumLoadedModules)
                    .Select(_target.GetModuleAtIndex)
                    .Where(m => m != null)
                    .ToList(),
                symbolsSettings, useSymbolStores, task, moduleFileLoadRecorder);
        }

        public IList<IDebugModule3> GetModulesByName(string moduleName)
        {
            return Enumerable.Range(0, NumLoadedModules)
                .Select(_target.GetModuleAtIndex)
                .Where(m => string.Equals(moduleName, m?.GetFileSpec()?.GetFilename(),
                                          StringComparison.OrdinalIgnoreCase))
                .Select(m => _debugModuleCache.GetOrCreate(m, _debugProgram))
                .ToList();
        }

        public void SetExceptions(IEnumerable<EXCEPTION_INFO> exceptions) =>
            _exceptionManager.SetExceptions(exceptions);

        public uint RemotePid { get; }

        void UpdateModulesList() => _debugProgram.EnumModules(out _);

        /// <summary>
        /// If a remote breakpoint gets bound, binds local breakpoint as well.
        /// </summary>
        void OnBreakpointChanged(object sender, BreakpointChangedEventArgs args)
        {
            IEventBreakpointData breakpointData = args.Event.BreakpointData;
            if (breakpointData.EventType.HasFlag(BreakpointEventType.LOCATIONS_ADDED) ||
                breakpointData.EventType.HasFlag(BreakpointEventType.LOCATIONS_REMOVED))
            {
                if (_breakpointManager.GetPendingBreakpointById(
                    breakpointData.BreakpointId, out IPendingBreakpoint pendingBreakpoint))
                {
                    pendingBreakpoint.UpdateLocations();
                }
            }
        }
    }
}

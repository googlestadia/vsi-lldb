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
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using YetiCommon;
using YetiCommon.CastleAspects;
using YetiVSI.DebugEngine.Exit;
using YetiVSI.Util;

namespace YetiVSI.DebugEngine
{
    public interface IGgpDebugProgram : IDebugProgram3, IDebugMemoryBytes2
    {
        /// <summary>
        /// Returns the DebugThread corresponding to an LLDBThread. If a cached DebugThread exists
        /// with matching ID, that is returned. Otherwise, a new
        /// DebugThread is created and added to the cache.
        /// </summary>
        IDebugThread GetDebugThread(RemoteThread lldbThread);

        /// <summary>
        /// Returns true if Visual Studio has called Terminate().
        /// </summary>
        bool TerminationRequested { get; }

        /// <summary>
        /// Returns true if Visual Studio has called Detach().
        /// </summary>
        bool DetachRequested { get; }
    }

    public interface IDebugProgramFactory
    {
        IGgpDebugProgram Create(IDebugEngineHandler debugEngineHandler,
            DebugProgram.ThreadCreator threadCreator,
            IDebugProcess2 process, Guid programId, SbProcess lldbProcess,
            RemoteTarget lldbTarget,
            IDebugModuleCache debugModuleCache, bool isCoreAttach);
    }

    // DebugProgram contains execution information about a process.
    public class DebugProgram : SimpleDecoratorSelf<IGgpDebugProgram>, IGgpDebugProgram
    {
        public delegate IDebugThread ThreadCreator(RemoteThread lldbThread,
            IGgpDebugProgram debugProgram);

        public class Factory : IDebugProgramFactory
        {
            readonly JoinableTaskContext _taskContext;
            readonly DebugDisassemblyStream.Factory _debugDisassemblyStreamFactory;
            readonly DebugDocumentContext.Factory _documentContextFactory;
            readonly DebugCodeContext.Factory _codeContextFactory;

            readonly ThreadEnumFactory _threadsEnumFactory;
            readonly ModuleEnumFactory _moduleEnumFactory;
            readonly CodeContextEnumFactory _codeContextEnumFactory;

            public Factory(JoinableTaskContext taskContext,
                DebugDisassemblyStream.Factory debugDisassemblyStreamFactory,
                DebugDocumentContext.Factory documentContextFactory,
                DebugCodeContext.Factory codeContextFactory,
                ThreadEnumFactory threadsEnumFactory,
                ModuleEnumFactory moduleEnumFactory,
                CodeContextEnumFactory codeContextEnumFactory)
            {
                _taskContext = taskContext;
                _debugDisassemblyStreamFactory = debugDisassemblyStreamFactory;
                _documentContextFactory = documentContextFactory;
                _codeContextFactory = codeContextFactory;
                _threadsEnumFactory = threadsEnumFactory;
                _moduleEnumFactory = moduleEnumFactory;
                _codeContextEnumFactory = codeContextEnumFactory;
            }

            public IGgpDebugProgram Create(IDebugEngineHandler debugEngineHandler,
                ThreadCreator threadCreator,
                IDebugProcess2 process, Guid programId, SbProcess lldbProcess,
                RemoteTarget lldbTarget,
                IDebugModuleCache debugModuleCache, bool isCoreAttach)
            {
                return new DebugProgram(_taskContext, threadCreator,
                    _debugDisassemblyStreamFactory,
                    _documentContextFactory, _codeContextFactory, _threadsEnumFactory,
                    _moduleEnumFactory, _codeContextEnumFactory, debugEngineHandler, process,
                    programId, lldbProcess, lldbTarget, debugModuleCache, isCoreAttach);
            }
        }

        readonly Guid _id;
        readonly IDebugProcess2 _process;
        readonly SbProcess _lldbProcess;
        readonly RemoteTarget _lldbTarget;
        readonly Dictionary<uint, IDebugThread> _threadCache;
        readonly bool _isCoreAttach;
        readonly IDebugEngineHandler _debugEngineHandler;

        readonly JoinableTaskContext _taskContext;
        readonly ThreadCreator _threadCreator;
        readonly DebugDisassemblyStream.Factory _debugDisassemblyStreamFactory;
        readonly DebugDocumentContext.Factory _documentContextFactory;
        readonly DebugCodeContext.Factory _codeContextFactory;
        readonly IDebugModuleCache _debugModuleCache;

        readonly ThreadEnumFactory _threadEnumFactory;
        readonly ModuleEnumFactory _moduleEnumFactory;
        readonly CodeContextEnumFactory _codeContextEnumFactory;

        DebugProgram(
            JoinableTaskContext taskContext,
            ThreadCreator threadCreator,
            DebugDisassemblyStream.Factory debugDisassemblyStreamFactory,
            DebugDocumentContext.Factory documentContextFactory,
            DebugCodeContext.Factory codeContextFactory,
            ThreadEnumFactory threadEnumFactory,
            ModuleEnumFactory moduleEnumFactory,
            CodeContextEnumFactory codeContextEnumFactory,
            IDebugEngineHandler debugEngineHandler,
            IDebugProcess2 process,
            Guid programId,
            SbProcess lldbProcess,
            RemoteTarget lldbTarget,
            IDebugModuleCache debugModuleCache,
            bool isCoreAttach)
        {
            _id = programId;
            _process = process;
            _lldbProcess = lldbProcess;
            _lldbTarget = lldbTarget;
            _threadCache = new Dictionary<uint, IDebugThread>();
            _isCoreAttach = isCoreAttach;
            _debugEngineHandler = debugEngineHandler;
            _taskContext = taskContext;
            _threadCreator = threadCreator;
            _debugDisassemblyStreamFactory = debugDisassemblyStreamFactory;
            _documentContextFactory = documentContextFactory;
            _codeContextFactory = codeContextFactory;
            _threadEnumFactory = threadEnumFactory;
            _moduleEnumFactory = moduleEnumFactory;
            _codeContextEnumFactory = codeContextEnumFactory;
            _debugModuleCache = debugModuleCache;
        }

        #region IGgpDebugProgram functions

        public IDebugThread GetDebugThread(RemoteThread lldbThread)
        {
            if (lldbThread == null)
            {
                return null;
            }
            lock (_threadCache)
            {
                IDebugThread debugThread;
                uint threadId = (uint)lldbThread.GetThreadId();
                if (!_threadCache.TryGetValue(threadId, out debugThread))
                {
                    debugThread = _threadCreator(lldbThread, Self);
                    _threadCache.Add(threadId, debugThread);
                    _debugEngineHandler.SendEvent(new ThreadCreateEvent(), Self, debugThread);
                }
                return debugThread;
            }
        }

        public bool TerminationRequested { get; private set; }

        public bool DetachRequested { get; private set; }

        #endregion

        #region IDebugProgram3 functions

        public int Attach(IDebugEventCallback2 callback)
        {
            return VSConstants.E_NOTIMPL;
        }

        public int CanDetach()
        {
            return VSConstants.S_OK;
        }

        public int CauseBreak()
        {
            if (_lldbProcess.Stop())
            {
                return VSConstants.S_OK;
            }
            return VSConstants.E_FAIL;
        }

        public int Continue(IDebugThread2 thread)
        {
            if (_isCoreAttach)
            {
                return AD7Constants.E_CRASHDUMP_UNSUPPORTED;
            }
            if (_lldbProcess.Continue())
            {
                return VSConstants.S_OK;
            }
            return VSConstants.E_FAIL;
        }

        public int Detach()
        {
            DetachRequested = true;
            if (!_lldbProcess.Detach())
            {
                DetachRequested = false;
                return VSConstants.E_FAIL;
            }

            // Send the ProgramDestroyEvent immediately, so that Visual Studio can't cause a
            // deadlock by waiting for the event while preventing us from accessing the main thread.
            // TODO: Block and wait for LldbEventHandler to send the event
            _debugEngineHandler.Abort(this, ExitInfo.Normal(ExitReason.DebuggerDetached));
            return VSConstants.S_OK;
        }

        public int EnumCodeContexts(
            IDebugDocumentPosition2 docPos, out IEnumDebugCodeContexts2 contextsEnum)
        {
            contextsEnum = null;
            var startPositions = new TEXT_POSITION[1];
            var result = docPos.GetRange(startPositions, null);
            if (result != VSConstants.S_OK)
            {
                Trace.WriteLine("Error: Unable to retrieve starting position.");
                return result;
            }
            string fileName;
            docPos.GetFileName(out fileName);
            var codeContexts = new List<IDebugCodeContext2>();

            // TODO: Find a less hacky way of doing this
            var tempBreakpoint = _lldbTarget.BreakpointCreateByLocation(fileName,
                        startPositions[0].dwLine + 1);
            if (tempBreakpoint == null)
            {
                Trace.WriteLine("Error: Failed to set temporary breakpoint used to map document " +
                    "position to code contexts.");
                return VSConstants.E_FAIL;
            }
            try
            {
                var numLocations = tempBreakpoint.GetNumLocations();
                for (uint i = 0; i < numLocations; ++i)
                {
                    var location = tempBreakpoint.GetLocationAtIndex(i);
                    var address = location.GetAddress();
                    if (address != null)
                    {
                        var codeContext = _codeContextFactory.Create(
                            address.GetLoadAddress(_lldbTarget),
                            address.GetFunction().GetName(),
                            _documentContextFactory.Create(address.GetLineEntry()),
                            Guid.Empty);
                        codeContexts.Add(codeContext);
                    }
                    else
                    {
                        Trace.WriteLine("Warning: Failed to obtain address for code context " +
                            "from temporary breakpoint location. Code context skipped.");
                    }
                }
            }
            finally
            {
                _lldbTarget.BreakpointDelete(tempBreakpoint.GetId());
                tempBreakpoint = null;
            }

            contextsEnum = _codeContextEnumFactory.Create(codeContexts);
            return VSConstants.S_OK;
        }

        public int EnumCodePaths(
            string hint, IDebugCodeContext2 start, IDebugStackFrame2 frame, int source,
            out IEnumCodePaths2 pathsEnum, out IDebugCodeContext2 safety)
        {
            pathsEnum = null;
            safety = null;
            return VSConstants.E_NOTIMPL;
        }

        public int EnumModules(out IEnumDebugModules2 modulesEnum)
        {
            var sbModules = GetSbModules();
            _debugModuleCache.RemoveAllExcept(sbModules);
            var modules = sbModules.Select(m => _debugModuleCache.GetOrCreate(m, Self));
            modulesEnum = _moduleEnumFactory.Create(modules);
            return VSConstants.S_OK;
        }

        public int EnumThreads(out IEnumDebugThreads2 threadsEnum)
        {
            lock (_threadCache)
            {
                var remoteThreads = GetRemoteThreads();

                // If we fail to get the list of threads, return the current cache instead.  If we
                // send a ThreadCreateEvent, and then fail to return that thread on the next
                // EnumThreads call, that thread will be lost forever.
                if (remoteThreads.Count == 0)
                {
                    threadsEnum = _threadEnumFactory.Create(_threadCache.Values);
                    return VSConstants.S_OK;
                }

                // Update the thread cache, and remove any stale entries.
                var deadIds = _threadCache.Keys.ToList();
                foreach (var remoteThread in remoteThreads)
                {
                    var currentThread = GetDebugThread(remoteThread);
                    uint threadId;
                    currentThread.GetThreadId(out threadId);
                    deadIds.Remove(threadId);
                }
                foreach (var threadId in deadIds)
                {
                    IDebugThread thread;
                    if (_threadCache.TryGetValue(threadId, out thread))
                    {
                        _debugEngineHandler.SendEvent(new ThreadDestroyEvent(0), Self, thread);
                        _threadCache.Remove(threadId);
                    }
                }
                threadsEnum = _threadEnumFactory.Create(_threadCache.Values);
            }
            return VSConstants.S_OK;
        }

        public int Execute()
        {
            return VSConstants.E_NOTIMPL;
        }

        public int GetDebugProperty(out IDebugProperty2 property)
        {
            property = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetDisassemblyStream(
            enum_DISASSEMBLY_STREAM_SCOPE scope, IDebugCodeContext2 codeContext,
            out IDebugDisassemblyStream2 disassemblyStream)
        {
            disassemblyStream = _debugDisassemblyStreamFactory.Create(
                scope, codeContext, _lldbTarget);
            return VSConstants.S_OK;
        }

        public int GetENCUpdate(out object update)
        {
            update = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetEngineInfo(out string engine, out Guid engineGuid)
        {
            engine = YetiConstants.YetiTitle;
            engineGuid = YetiConstants.DebugEngineGuid;
            return VSConstants.S_OK;
        }

        public int GetMemoryBytes(out IDebugMemoryBytes2 memoryBytes)
        {
            memoryBytes = Self;
            return VSConstants.S_OK;
        }

        public int GetName(out string name)
        {
            _taskContext.ThrowIfNotOnMainThread();
            return _process.GetName(enum_GETNAME_TYPE.GN_BASENAME, out name);
        }

        public int GetProcess(out IDebugProcess2 process)
        {
            _taskContext.ThrowIfNotOnMainThread();
            process = _process;
            return VSConstants.S_OK;
        }

        public int GetProgramId(out Guid guid)
        {
            guid = _id;
            return VSConstants.S_OK;
        }

        public int Step(IDebugThread2 thread, enum_STEPKIND sk, enum_STEPUNIT step)
        {
            if (_isCoreAttach)
            {
                return AD7Constants.E_CRASHDUMP_UNSUPPORTED;
            }
            var lldbThread = ((IDebugThread)thread).GetRemoteThread();
            switch (step)
            {
                case enum_STEPUNIT.STEP_STATEMENT:
                    switch (sk)
                    {
                        case enum_STEPKIND.STEP_INTO:
                            lldbThread.StepInto();
                            break;
                        case enum_STEPKIND.STEP_OVER:
                            lldbThread.StepOver();
                            break;
                        case enum_STEPKIND.STEP_OUT:
                            lldbThread.StepOut();
                            break;
                        default:
                            return VSConstants.E_NOTIMPL;
                    }
                    return VSConstants.S_OK;
                case enum_STEPUNIT.STEP_INSTRUCTION:
                    switch (sk)
                    {
                        case enum_STEPKIND.STEP_OVER:
                            lldbThread.StepInstruction(true);
                            break;
                        case enum_STEPKIND.STEP_INTO:
                            lldbThread.StepInstruction(false);
                            break;
                        case enum_STEPKIND.STEP_OUT:
                            lldbThread.StepOut();
                            break;
                        default:
                            return VSConstants.E_NOTIMPL;
                    }
                    return VSConstants.S_OK;
            }
            return VSConstants.E_NOTIMPL;
        }

        public int Terminate()
        {
            TerminationRequested = true;
            //TODO: remove the legacy launch flow.
            if (!_lldbProcess.Kill())
            {
                // Visual Studio waits for the ProgramDestroyEvent regardless of whether
                // Terminate() succeeds, so we send the event on failure as well as on success.
                _debugEngineHandler.Abort(
                    this,
                    ExitInfo.Error(new TerminateProcessException(ErrorStrings.FailedToStopGame)));
                return VSConstants.E_FAIL;
            }

            // Send the ProgramDestroyEvent immediately, so that Visual Studio can't cause a
            // deadlock by waiting for the event while preventing us from accessing the main thread.
            // TODO: Block and wait for LldbEventHandler to send the event
            _debugEngineHandler.Abort(this, ExitInfo.Normal(ExitReason.DebuggerTerminated));
            return VSConstants.S_OK;
        }

        public int WriteDump(enum_DUMPTYPE dumpType, string dumpUrl)
        {
            return VSConstants.E_NOTIMPL;
        }

        // Confusingly, at least in the Attach case with LLDB, the SDM calls
        // this when continuing rather than Continue.
        public int ExecuteOnThread(IDebugThread2 pThread)
        {
            _taskContext.ThrowIfNotOnMainThread();
            return Continue(pThread);
        }

        #endregion

        // Gets a list of all the SbModules.
        List<SbModule> GetSbModules()
        {
            var modules = new List<SbModule>();
            var numModules = _lldbTarget.GetNumModules();
            for (int i = 0; i < numModules; i++)
            {
                var module = _lldbTarget.GetModuleAtIndex(i);
                if (module == null)
                {
                    Trace.WriteLine("Unable to retrieve module " + i);
                    continue;
                }
                modules.Add(module);
            }
            return modules;
        }

        // Gets a list of all the RemoteThreads. If there is an issue retreiving any of the threads,
        // instead of returning a partial list, an empty list is returned.
        List<RemoteThread> GetRemoteThreads()
        {
            var threads = new List<RemoteThread>();
            var numberThreads = _lldbProcess.GetNumThreads();
            for (int i = 0; i < numberThreads; i++)
            {
                var thread = _lldbProcess.GetThreadAtIndex(i);
                if (thread == null)
                {
                    Trace.WriteLine("Failed to get thread at index: " + i);
                    return new List<RemoteThread>();
                }
                threads.Add(thread);
            }
            return threads;
        }

        #region IDebugMemoryBytes2 functions

        public int ReadAt(IDebugMemoryContext2 startMemoryContext, uint countToRead, byte[] memory,
            out uint countRead, ref uint countUnreadable)
        {
            // |countUnreadable| can be null when calling from C++ according to Microsoft's doc.
            // However, countUnreadable == null will always return false in C# as an uint can never
            // be null. Accessing |countUnreadable| here might cause a NullReferenceException.
            // According to Microsoft's doc, |countUnreadable| is useful when there are
            // non -consecutive blocks of memory that are readable. For example, if we want to read
            // 100 bytes at a certain address and only the first 50 bytes and the last 20 bytes are
            // readable.  |countRead| would be 50 and |countUnreadable| should be 30. However,
            // LLDB's ReadMemory() doesn't provide this kind of output, so this is not
            // straightforward to implement.

            // TODO We should estimate |countUnreadable| from page boundaries.
            // Otherwise, the memory view might be missing some valid contents in the beginnings
            // of mapped regions.

            // Note: We need to make sure we never return S_OK while setting countRead and
            // countUnreadable to zero. That can send the memory view into an infinite loop
            // and freeze Visual Studio ((internal)).
            CONTEXT_INFO[] contextInfo = new CONTEXT_INFO[1];
            startMemoryContext.GetInfo(enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS, contextInfo);
            ulong address;
            if (DebugEngineUtil.GetAddressFromString(contextInfo[0].bstrAddress, out address))
            {
                SbError error;
                try
                {
                    countRead = Convert.ToUInt32(_lldbProcess.ReadMemory(address, memory,
                        countToRead, out error));
                    if (error.Fail() || countRead == 0)
                    {
                        countRead = 0;
                        return VSConstants.E_FAIL;
                    }
                    return VSConstants.S_OK;
                }
                catch (OverflowException e)
                {
                    Trace.WriteLine($"Warning: Failed to read memory.{Environment.NewLine}" +
                        $"{e.ToString()}");
                    countRead = 0;
                    return VSConstants.E_FAIL;
                }
            }
            countRead = 0;
            return VSConstants.E_FAIL;
       }

        public int WriteAt(IDebugMemoryContext2 startMemoryContext, uint count, byte[] buffer)
        {
            CONTEXT_INFO[] contextInfos = new CONTEXT_INFO[1];
            startMemoryContext.GetInfo(enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS, contextInfos);
            ulong address;
            if (!DebugEngineUtil.GetAddressFromString(contextInfos[0].bstrAddress, out address))
            {
                Trace.WriteLine($"Failed to convert {contextInfos[0].bstrAddress} to address");
                return VSConstants.E_FAIL;
            }
            SbError error;
            var bytesWrote = _lldbProcess.WriteMemory(address, buffer, count, out error);
            if (error.Fail())
            {
                Trace.WriteLine($"Error: {error.GetCString()}");
                return VSConstants.E_FAIL;
            }
            if (bytesWrote != count)
            {
                Trace.WriteLine(
                    $"Warning: only written {bytesWrote} out of {count} bytes to memory.");
                return VSConstants.S_FALSE;
            }
            return VSConstants.S_OK;
        }

        public int GetSize(out ulong size)
        {
            size = 0;
            return VSConstants.E_NOTIMPL;
        }

        #endregion

        public class TerminateProcessException : Exception, IUserVisibleError
        {
            public string UserDetails { get { return null; } }

            public TerminateProcessException(string message) : base(message) { }

            public TerminateProcessException(string message, Exception inner) : base(message, inner)
            { }
        }
    }
}

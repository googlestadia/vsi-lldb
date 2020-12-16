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
            private readonly JoinableTaskContext taskContext;
            private readonly DebugDisassemblyStream.Factory debugDisassemblyStreamFactory;
            private readonly DebugDocumentContext.Factory documentContextFactory;
            private readonly DebugCodeContext.Factory codeContextFactory;

            private readonly ThreadEnumFactory threadsEnumFactory;
            private readonly ModuleEnumFactory moduleEnumFactory;
            private readonly CodeContextEnumFactory codeContextEnumFactory;

            public Factory(JoinableTaskContext taskContext,
                DebugDisassemblyStream.Factory debugDisassemblyStreamFactory,
                DebugDocumentContext.Factory documentContextFactory,
                DebugCodeContext.Factory codeContextFactory,
                ThreadEnumFactory threadsEnumFactory,
                ModuleEnumFactory moduleEnumFactory,
                CodeContextEnumFactory codeContextEnumFactory)
            {
                this.taskContext = taskContext;
                this.debugDisassemblyStreamFactory = debugDisassemblyStreamFactory;
                this.documentContextFactory = documentContextFactory;
                this.codeContextFactory = codeContextFactory;
                this.threadsEnumFactory = threadsEnumFactory;
                this.moduleEnumFactory = moduleEnumFactory;
                this.codeContextEnumFactory = codeContextEnumFactory;
            }

            public IGgpDebugProgram Create(IDebugEngineHandler debugEngineHandler,
                ThreadCreator threadCreator,
                IDebugProcess2 process, Guid programId, SbProcess lldbProcess,
                RemoteTarget lldbTarget,
                IDebugModuleCache debugModuleCache, bool isCoreAttach)
            {
                return new DebugProgram(taskContext, threadCreator,
                    debugDisassemblyStreamFactory,
                    documentContextFactory, codeContextFactory, threadsEnumFactory,
                    moduleEnumFactory, codeContextEnumFactory, debugEngineHandler, process,
                    programId, lldbProcess, lldbTarget, debugModuleCache, isCoreAttach);
            }
        }

        Guid id;
        IDebugProcess2 process;
        SbProcess lldbProcess;
        RemoteTarget lldbTarget;
        Dictionary<uint, IDebugThread> threadCache;
        bool isCoreAttach;
        IDebugEngineHandler debugEngineHandler;

        JoinableTaskContext taskContext;
        ThreadCreator threadCreator;
        DebugDisassemblyStream.Factory debugDisassemblyStreamFactory;
        DebugDocumentContext.Factory documentContextFactory;
        DebugCodeContext.Factory codeContextFactory;
        IDebugModuleCache debugModuleCache;

        ThreadEnumFactory threadEnumFactory;
        ModuleEnumFactory moduleEnumFactory;
        CodeContextEnumFactory codeContextEnumFactory;

        private DebugProgram(
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
            id = programId;
            this.process = process;
            this.lldbProcess = lldbProcess;
            this.lldbTarget = lldbTarget;
            threadCache = new Dictionary<uint, IDebugThread>();
            this.isCoreAttach = isCoreAttach;
            this.debugEngineHandler = debugEngineHandler;
            this.taskContext = taskContext;
            this.threadCreator = threadCreator;
            this.debugDisassemblyStreamFactory = debugDisassemblyStreamFactory;
            this.documentContextFactory = documentContextFactory;
            this.codeContextFactory = codeContextFactory;
            this.threadEnumFactory = threadEnumFactory;
            this.moduleEnumFactory = moduleEnumFactory;
            this.codeContextEnumFactory = codeContextEnumFactory;
            this.debugModuleCache = debugModuleCache;
        }

        #region IGgpDebugProgram functions

        public IDebugThread GetDebugThread(RemoteThread lldbThread)
        {
            if (lldbThread == null)
            {
                return null;
            }
            lock (threadCache)
            {
                IDebugThread debugThread;
                uint threadId = (uint)lldbThread.GetThreadId();
                if (!threadCache.TryGetValue(threadId, out debugThread))
                {
                    debugThread = threadCreator(lldbThread, Self);
                    threadCache.Add(threadId, debugThread);
                    debugEngineHandler.SendEvent(new ThreadCreateEvent(), Self, debugThread);
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
            if (lldbProcess.Stop())
            {
                return VSConstants.S_OK;
            }
            return VSConstants.E_FAIL;
        }

        public int Continue(IDebugThread2 thread)
        {
            if (isCoreAttach)
            {
                return AD7Constants.E_CRASHDUMP_UNSUPPORTED;
            }
            if (lldbProcess.Continue())
            {
                return VSConstants.S_OK;
            }
            return VSConstants.E_FAIL;
        }

        public int Detach()
        {
            DetachRequested = true;
            if (!lldbProcess.Detach())
            {
                DetachRequested = false;
                return VSConstants.E_FAIL;
            }

            // Send the ProgramDestroyEvent immediately, so that Visual Studio can't cause a
            // deadlock by waiting for the event while preventing us from accessing the main thread.
            // TODO: Block and wait for LldbEventHandler to send the event
            debugEngineHandler.Abort(this, ExitInfo.Normal(ExitReason.DebuggerDetached));
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
            var tempBreakpoint = lldbTarget.BreakpointCreateByLocation(fileName,
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
                        var codeContext = codeContextFactory.Create(
                            address.GetLoadAddress(lldbTarget),
                            address.GetFunction().GetName(),
                            documentContextFactory.Create(address.GetLineEntry()),
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
                lldbTarget.BreakpointDelete(tempBreakpoint.GetId());
                tempBreakpoint = null;
            }

            contextsEnum = codeContextEnumFactory.Create(codeContexts);
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
            debugModuleCache.RemoveAllExcept(sbModules);
            var modules = sbModules.Select(m => debugModuleCache.GetOrCreate(m, Self));
            modulesEnum = moduleEnumFactory.Create(modules);
            return VSConstants.S_OK;
        }

        public int EnumThreads(out IEnumDebugThreads2 threadsEnum)
        {
            lock (threadCache)
            {
                var remoteThreads = GetRemoteThreads();

                // If we fail to get the list of threads, return the current cache instead.  If we
                // send a ThreadCreateEvent, and then fail to return that thread on the next
                // EnumThreads call, that thread will be lost forever.
                if (remoteThreads.Count == 0)
                {
                    threadsEnum = threadEnumFactory.Create(threadCache.Values);
                    return VSConstants.S_OK;
                }

                // Update the thread cache, and remove any stale entries.
                var deadIds = threadCache.Keys.ToList();
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
                    if (threadCache.TryGetValue(threadId, out thread))
                    {
                        debugEngineHandler.SendEvent(new ThreadDestroyEvent(0), Self, thread);
                        threadCache.Remove(threadId);
                    }
                }
                threadsEnum = threadEnumFactory.Create(threadCache.Values);
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
            disassemblyStream = debugDisassemblyStreamFactory.Create(
                scope, codeContext, lldbTarget);
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
            taskContext.ThrowIfNotOnMainThread();
            return process.GetName(enum_GETNAME_TYPE.GN_BASENAME, out name);
        }

        public int GetProcess(out IDebugProcess2 process)
        {
            taskContext.ThrowIfNotOnMainThread();
            process = this.process;
            return VSConstants.S_OK;
        }

        public int GetProgramId(out Guid guid)
        {
            guid = this.id;
            return VSConstants.S_OK;
        }

        public int Step(IDebugThread2 thread, enum_STEPKIND sk, enum_STEPUNIT step)
        {
            if (isCoreAttach)
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
            if (!lldbProcess.Kill())
            {
                // Visual Studio waits for the ProgramDestroyEvent regardless of whether Terminate()
                // succeeds, so we send the event on failure as well as on success.
                debugEngineHandler.Abort(this,
                    ExitInfo.Error(new TerminateProcessException(ErrorStrings.FailedToStopGame)));
                return VSConstants.E_FAIL;
            }

            // Send the ProgramDestroyEvent immediately, so that Visual Studio can't cause a
            // deadlock by waiting for the event while preventing us from accessing the main thread.
            // TODO: Block and wait for LldbEventHandler to send the event
            debugEngineHandler.Abort(this, ExitInfo.Normal(ExitReason.DebuggerTerminated));
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
            taskContext.ThrowIfNotOnMainThread();
            return Continue(pThread);
        }

        #endregion

        // Gets a list of all the SbModules.
        List<SbModule> GetSbModules()
        {
            var modules = new List<SbModule>();
            var numModules = lldbTarget.GetNumModules();
            for (int i = 0; i < numModules; i++)
            {
                var module = lldbTarget.GetModuleAtIndex(i);
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
            var numberThreads = lldbProcess.GetNumThreads();
            for (int i = 0; i < numberThreads; i++)
            {
                var thread = lldbProcess.GetThreadAtIndex(i);
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
                    countRead = Convert.ToUInt32(lldbProcess.ReadMemory(address, memory,
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
            var bytesWrote = lldbProcess.WriteMemory(address, buffer, count, out error);
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

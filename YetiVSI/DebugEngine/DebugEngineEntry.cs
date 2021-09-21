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

using Microsoft.VisualStudio.Debugger.Interop;
using System;
using System.Runtime.InteropServices;
using YetiVSI.DebugEngine.CastleAspects;
using YetiVSI.Util;

namespace YetiVSI.DebugEngine
{
    // Our aspect framework requires the factory Create() function to return an interface.
    public interface IGgpDebugEngine : IDebugEngine3, IDebugEngineLaunch2
    {
        /// <summary>
        /// Called when the debug session is ending.
        ///
        /// *** IMPORTANT ***
        /// This is called on a best effort basis and it is not guaranteed to be called.
        /// </summary>
        event EventHandler SessionEnding;

        /// <summary>
        /// Called when the debug session has ended.
        /// </summary>
        event EventHandler SessionEnded;

        /// <summary>
        /// Debug Engine identifier for logging purposes.
        ///
        /// *** IMPORTANT *** Not guaranteed to be unique.
        /// </summary>
        Guid Id { get; }

        IDebugEngineCommands DebugEngineCommands { get; }

        [InteropBoundary]
        int SetEngineGuidImpl(Guid guidEngine);
    }

    // Methods of the IDebugEngine2 and IDebugEngineLaunch2 interfaces are called
    // by the Visual Studio SDM (session debug manager).
    //
    // The SDM expects to receive events from the debug engine in a certain order.
    // Reference:
    // https://docs.microsoft.com/en-us/visualstudio/extensibility/debugger/sending-the-required-events
    [Guid("011ff79f-0dd3-4d89-8863-5f0761243e74")]
    public class DebugEngineEntry : IGgpDebugEngine
    {
        public event EventHandler SessionEnding;
        public event EventHandler SessionEnded;

        readonly IGgpDebugEngine debugEngine;

        public DebugEngineEntry()
        {
            var compRoot = new DebugEngineFactoryCompRoot();
            compRoot.GetJoinableTaskContext().ThrowIfNotOnMainThread();

            // DebugEngineEntry is the external 'identity' of the debug engine, not the proxy or
            // inner DebugEngine class.  Visual Studio will not work properly if we expose
            // something other than 'this'.
            debugEngine = compRoot.CreateDebugEngineFactory().Create(this);

            debugEngine.SessionEnding += DebugEngine_SessionEnding;
            debugEngine.SessionEnded += DebugEngine_SessionEnded;

            var debugEngineManager =
                compRoot.CreateServiceManager().GetGlobalService(typeof(SDebugEngineManager)) as
                    IDebugEngineManager;
            debugEngineManager?.AddDebugEngine(this);
        }

        #region IDebugEngine3

        public int Attach(IDebugProgram2[] rgpPrograms, IDebugProgramNode2[] rgpProgramNodes,
                          uint celtPrograms, IDebugEventCallback2 pCallback,
                          enum_ATTACH_REASON dwReason) =>
            debugEngine.Attach(rgpPrograms, rgpProgramNodes, celtPrograms, pCallback, dwReason);

        public int CauseBreak() => debugEngine.CauseBreak();

        public int ContinueFromSynchronousEvent(IDebugEvent2 pEvent) =>
            debugEngine.ContinueFromSynchronousEvent(pEvent);

        public int CreatePendingBreakpoint(IDebugBreakpointRequest2 pBPRequest,
                                           out IDebugPendingBreakpoint2 ppPendingBP) =>
            debugEngine.CreatePendingBreakpoint(pBPRequest, out ppPendingBP);

        public int DestroyProgram(IDebugProgram2 pProgram) => debugEngine.DestroyProgram(pProgram);

        public int EnumPrograms(out IEnumDebugPrograms2 ppEnum) =>
            debugEngine.EnumPrograms(out ppEnum);

        public int GetEngineId(out Guid pguidEngine) => debugEngine.GetEngineId(out pguidEngine);

        public int LoadSymbols() => debugEngine.LoadSymbols();

        public int RemoveAllSetExceptions(ref Guid guidType) =>
            debugEngine.RemoveAllSetExceptions(ref guidType);

        public int RemoveSetException(EXCEPTION_INFO[] pException) =>
            debugEngine.RemoveSetException(pException);

        public int SetAllExceptions(enum_EXCEPTION_STATE dwState) =>
            debugEngine.SetAllExceptions(dwState);

        public int SetEngineGuid(ref Guid guidEngine) => debugEngine.SetEngineGuid(ref guidEngine);

        public int SetEngineGuidImpl(Guid guidEngine) => debugEngine.SetEngineGuidImpl(guidEngine);

        public int SetException(EXCEPTION_INFO[] pException) =>
            debugEngine.SetException(pException);

        public int SetJustMyCodeState(int fUpdate, uint dwModules, JMC_CODE_SPEC[] rgJMCSpec) =>
            debugEngine.SetJustMyCodeState(fUpdate, dwModules, rgJMCSpec);

        public int SetLocale(ushort wLangID) => debugEngine.SetLocale(wLangID);

        public int SetMetric(string pszMetric, object varValue) =>
            debugEngine.SetMetric(pszMetric, varValue);

        public int SetRegistryRoot(string pszRegistryRoot) =>
            debugEngine.SetRegistryRoot(pszRegistryRoot);

        public int SetSymbolPath(string szSymbolSearchPath, string szSymbolCachePath, uint Flags) =>
            debugEngine.SetSymbolPath(szSymbolSearchPath, szSymbolCachePath, Flags);

        #endregion

        #region IDebugEngineLaunch2

        public int CanTerminateProcess(IDebugProcess2 pProcess) =>
            debugEngine.CanTerminateProcess(pProcess);

        public int LaunchSuspended(string pszServer, IDebugPort2 pPort, string pszExe,
                                   string pszArgs, string pszDir, string bstrEnv, string pszOptions,
                                   enum_LAUNCH_FLAGS dwLaunchFlags, uint hStdInput, uint hStdOutput,
                                   uint hStdError, IDebugEventCallback2 pCallback,
                                   out IDebugProcess2 ppProcess) =>
            debugEngine.LaunchSuspended(pszServer, pPort, pszExe, pszArgs, pszDir, bstrEnv,
                                        pszOptions, dwLaunchFlags, hStdInput, hStdOutput, hStdError,
                                        pCallback, out ppProcess);

        public int ResumeProcess(IDebugProcess2 pProcess) => debugEngine.ResumeProcess(pProcess);

        public int TerminateProcess(IDebugProcess2 pProcess) =>
            debugEngine.TerminateProcess(pProcess);

        #endregion

        #region IGgpDebugEngine

        public Guid Id => debugEngine.Id;

        public IDebugEngineCommands DebugEngineCommands => debugEngine.DebugEngineCommands;

        void DebugEngine_SessionEnding(object sender, EventArgs args)
        {
            ((IGgpDebugEngine) sender).SessionEnding -= DebugEngine_SessionEnding;
            SessionEnding?.Invoke(this, args);
        }

        void DebugEngine_SessionEnded(object sender, EventArgs args)
        {
            ((IGgpDebugEngine) sender).SessionEnded -= DebugEngine_SessionEnded;
            SessionEnded?.Invoke(this, args);
        }

        #endregion
    }
}
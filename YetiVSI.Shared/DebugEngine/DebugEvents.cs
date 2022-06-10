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
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using YetiVSI.DebugEngine.Exit;
using YetiCommon;

namespace YetiVSI.DebugEngine
{
    class LoadCompleteEvent :
        StoppingDebugEvent<IDebugLoadCompleteEvent2>, IDebugLoadCompleteEvent2
    {
    }

    class EntryPointEvent :
        StoppingDebugEvent<IDebugEntryPointEvent2>, IDebugEntryPointEvent2
    {
    }

    public class EngineCreateEvent :
        SyncDebugEvent<IDebugEngineCreateEvent2>, IDebugEngineCreateEvent2
    {
        readonly IDebugEngine2 _engine;

        public EngineCreateEvent(IDebugEngine2 engine)
        {
            _engine = engine;
        }

        public int GetEngine(out IDebugEngine2 pEngine)
        {
            pEngine = _engine;
            return VSConstants.S_OK;
        }
    }

    public class ProgramCreateEvent :
        SyncDebugEvent<IDebugProgramCreateEvent2>, IDebugProgramCreateEvent2
    {
    }

    public class ProgramDestroyEvent :
        SyncDebugEvent<IDebugProgramDestroyEvent2>, IDebugProgramDestroyEvent2
    {
        readonly uint _exitCode = 0;

        public ProgramDestroyEvent(ExitInfo exitInfo)
        {
            ExitInfo = exitInfo;
        }

        public int GetExitCode(out uint exitCode)
        {
            exitCode = _exitCode;
            return VSConstants.S_OK;
        }

        public ExitInfo ExitInfo { get; }
    }

    public class ThreadCreateEvent :
        SyncDebugEvent<IDebugThreadCreateEvent2>, IDebugThreadCreateEvent2
    {
    }

    public class ThreadDestroyEvent :
        SyncDebugEvent<IDebugThreadDestroyEvent2>, IDebugThreadDestroyEvent2
    {
        readonly uint _exitCode;

        public ThreadDestroyEvent(uint exitCode)
        {
            _exitCode = exitCode;
        }

        public int GetExitCode(out uint exitCode)
        {
            exitCode = _exitCode;
            return VSConstants.S_OK;
        }
    }

    public class BreakEvent :
        StoppingDebugEvent<IDebugBreakEvent2>, IDebugBreakEvent2
    {
    }

    public class StepCompleteEvent :
        StoppingDebugEvent<IDebugStepCompleteEvent2>, IDebugStepCompleteEvent2
    {
    }

    public class BreakpointEvent :
        StoppingDebugEvent<IDebugBreakpointEvent2>, IDebugBreakpointEvent2
    {
        readonly IEnumDebugBoundBreakpoints2 _breakpointEnum;

        public BreakpointEvent(IEnumDebugBoundBreakpoints2 breakpointEnum)
        {
            _breakpointEnum = breakpointEnum;
        }

        public int EnumBreakpoints(out IEnumDebugBoundBreakpoints2 breakpointEnum)
        {
            breakpointEnum = _breakpointEnum;
            return VSConstants.S_OK;
        }
    }

    public class BreakpointErrorEvent :
        SyncDebugEvent<IDebugBreakpointErrorEvent2>, IDebugBreakpointErrorEvent2
    {
        readonly DebugBreakpointError _breakpointError;

        public BreakpointErrorEvent(DebugBreakpointError breakpointError)
        {
            _breakpointError = breakpointError;
        }

        public int GetErrorBreakpoint(out IDebugErrorBreakpoint2 breakpointError)
        {
            breakpointError = _breakpointError;
            return VSConstants.S_OK;
        }
    }

    public class BreakpointBoundEvent :
        SyncDebugEvent<IDebugBreakpointBoundEvent2>, IDebugBreakpointBoundEvent2
    {
        readonly IDebugPendingBreakpoint2 _pendingBreakpoint;
        readonly IEnumerable<IDebugBoundBreakpoint2> _newlyBoundBreakpoints;
        readonly BoundBreakpointEnumFactory _breakpointBoundEnumFactory;

        public BreakpointBoundEvent(IDebugPendingBreakpoint2 pendingBreakpoint)
        {
            _pendingBreakpoint = pendingBreakpoint;
            _newlyBoundBreakpoints = null;
            _breakpointBoundEnumFactory = null;
        }

        public BreakpointBoundEvent(
            IDebugPendingBreakpoint2 pendingBreakpoint,
            IEnumerable<IDebugBoundBreakpoint2> newlyBoundBreakpoints,
            BoundBreakpointEnumFactory breakpointBoundEnumFactory)
        {
            _pendingBreakpoint = pendingBreakpoint;
            _newlyBoundBreakpoints = newlyBoundBreakpoints;
            _breakpointBoundEnumFactory = breakpointBoundEnumFactory;
        }

        public int EnumBoundBreakpoints(out IEnumDebugBoundBreakpoints2 boundBreakpointsEnum)
        {
            if (_newlyBoundBreakpoints != null && _breakpointBoundEnumFactory != null)
            {
                boundBreakpointsEnum = _breakpointBoundEnumFactory.Create(_newlyBoundBreakpoints);
            }
            else
            {
                _pendingBreakpoint.EnumBoundBreakpoints(out boundBreakpointsEnum);
            }
            return VSConstants.S_OK;
        }

        public int GetPendingBreakpoint(out IDebugPendingBreakpoint2 pendingBreakpoint)
        {
            pendingBreakpoint = _pendingBreakpoint;
            return VSConstants.S_OK;
        }
    }

    public class DebugExpressionEvaluationCompleteEvent :
        AsyncDebugEvent<IDebugExpressionEvaluationCompleteEvent2>,
        IDebugExpressionEvaluationCompleteEvent2
    {
        readonly IDebugExpression2 _expr;
        readonly IDebugProperty2 _result;

        public DebugExpressionEvaluationCompleteEvent(IDebugExpression2 expr,
                                                      IDebugProperty2 result)
        {
            _expr = expr;
            _result = result;
        }

        public int GetExpression(out IDebugExpression2 expr)
        {
            expr = _expr;
            return VSConstants.S_OK;
        }

        public int GetResult(out IDebugProperty2 result)
        {
            result = _result;
            return VSConstants.S_OK;
        }
    }

    public class ExceptionEvent : StoppingDebugEvent<IDebugExceptionEvent2>, IDebugExceptionEvent2
    {
        readonly string _exceptionName;
        readonly string _detail;
        readonly uint _code;
        readonly enum_EXCEPTION_STATE _state;

        public ExceptionEvent(string exceptionName, uint code, enum_EXCEPTION_STATE state,
                              string description)
        {
            _exceptionName = exceptionName;
            _code = code;
            _state = state;
            _detail = description;
        }

        public int CanPassToDebuggee() => VSConstants.S_FALSE;

        public int GetException(EXCEPTION_INFO[] pExceptionInfo)
        {
            if (pExceptionInfo.Length == 0)
            {
                return VSConstants.E_INVALIDARG;
            }

            pExceptionInfo[0] =
                new EXCEPTION_INFO { bstrExceptionName = _exceptionName, dwCode = _code,
                                     dwState = _state, guidType = EventId };
            return VSConstants.S_OK;
        }

        public int GetExceptionDescription(out string description)
        {
            description = "";
            if (!string.IsNullOrEmpty(_exceptionName))
            {
                description = _exceptionName + ": ";
            }

            description += _detail;
            return VSConstants.S_OK;
        }

        public int PassToDebuggee(int fPass) => VSConstants.E_FAIL;
    }

    public class DebugModuleLoadEvent :
        AsyncDebugEvent<IDebugModuleLoadEvent2>, IDebugModuleLoadEvent2
    {
        readonly IDebugModule2 _module;
        readonly bool _loaded; // true if a module was loaded, false if a module was unloaded

        public DebugModuleLoadEvent(IDebugModule2 module, bool loaded)
        {
            _module = module;
            _loaded = loaded;
        }

        public int GetModule(out IDebugModule2 pModule, ref string pbstrDebugMessage,
                             ref int pbLoad)
        {
            pModule = _module;
            pbLoad = _loaded ? 1 : 0;
            return VSConstants.S_OK;
        }
    }

    public class DebugSymbolSearchEvent :
        AsyncDebugEvent<IDebugSymbolSearchEvent2>, IDebugSymbolSearchEvent2
    {
        readonly IDebugModule3 _module;
        readonly string _moduleName;
        readonly string _errorMessage;
        readonly bool _loaded;

        public DebugSymbolSearchEvent(IDebugModule3 module, string moduleName, string errorMessage,
                                      bool loaded)
        {
            _module = module;
            _moduleName = moduleName;
            _errorMessage = errorMessage;
            _loaded = loaded;
        }

        // Haven't seen this method being actually invoked, VS asks IDebugModule3 directly.
        public int GetSymbolSearchInfo(out IDebugModule3 pModule, ref string pbstrDebugMessage,
                                       enum_MODULE_INFO_FLAGS[] pdwModuleInfoFlags)
        {
            pModule = _module;

            // From documentation: Returns a string containing any error messages from the
            // module. If there is no error, then this string will just contain the module's
            // name but it is never empty.
            // https://docs.microsoft.com/en-us/visualstudio/extensibility/debugger/reference/idebugsymbolsearchevent2-getsymbolsearchinfo?view=vs-2019
            pbstrDebugMessage = _errorMessage ?? _moduleName;

            if (pdwModuleInfoFlags.Length > 0)
            {
                pdwModuleInfoFlags[0] = _loaded ? enum_MODULE_INFO_FLAGS.MIF_SYMBOLS_LOADED : 0;
            }

            return VSConstants.S_OK;
        }
    }

    public class DebugProcessInfoUpdatedEvent :
        AsyncDebugEvent<IDebugProcessInfoUpdatedEvent158>, IDebugProcessInfoUpdatedEvent158
    {
        readonly uint _processId;

        public DebugProcessInfoUpdatedEvent(uint processId)
        {
            _processId = processId;
        }

        public int GetUpdatedProcessInfo(out string pbstrName, out uint pdwSystemProcessId)
        {
            // For some reason `pbstrName` doesn't affect anything.
            pbstrName = null;
            pdwSystemProcessId = _processId;
            return 0;
        }
    }

    public interface IGgpDebugEvent : IDebugEvent2
    {
        Guid EventId { get; }
    }

    public abstract class DebugEvent<T> : IGgpDebugEvent
    {
        public Guid EventId => typeof(T).GUID;

        protected abstract uint Attributes { get; }

        int IDebugEvent2.GetAttributes(out uint attributes)
        {
            attributes = Attributes;
            return VSConstants.S_OK;
        }
    }

    public class SyncDebugEvent<T> : DebugEvent<T>
    {
        protected override uint Attributes
        {
            get { return (uint)enum_EVENTATTRIBUTES.EVENT_SYNCHRONOUS; }
        }
    }

    public class AsyncDebugEvent<T> : DebugEvent<T>
    {
        protected override uint Attributes
        {
            get { return (uint)enum_EVENTATTRIBUTES.EVENT_ASYNCHRONOUS; }
        }
    }

    public class StoppingDebugEvent<T> : DebugEvent<T>
    {
        protected override uint Attributes
        {
            get { return (uint)enum_EVENTATTRIBUTES.EVENT_STOPPING; }
        }
    }
}
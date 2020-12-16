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
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using YetiVSI.DebugEngine.Exit;
using YetiCommon;

namespace YetiVSI.DebugEngine
{
    // The event interface GUIDs can be extracted from
    // C:\Program Files (x86)\Microsoft Visual Studio 14.0\VSSDK\VisualStudioIntegration\Common\IDL\msdbg.idl

    class LoadCompleteEvent : DebugEvent, IDebugLoadCompleteEvent2
    {
        public LoadCompleteEvent()
            : base((uint)(enum_EVENTATTRIBUTES.EVENT_STOPPING |
                          enum_EVENTATTRIBUTES.EVENT_SYNCHRONOUS),
                   new Guid("B1844850-1349-45D4-9F12-495212F5EB0B"))
        {
        }
    }

    class EntryPointEvent : DebugEvent, IDebugEntryPointEvent2
    {
        public EntryPointEvent()
            : base((uint)(enum_EVENTATTRIBUTES.EVENT_STOPPING |
                          enum_EVENTATTRIBUTES.EVENT_SYNCHRONOUS),
                   new Guid("E8414A3E-1642-48EC-829E-5F4040E16DA9"))
        {
        }
    }

    public class EngineCreateEvent : DebugEvent, IDebugEngineCreateEvent2
    {
        readonly IDebugEngine2 _engine;

        public EngineCreateEvent(IDebugEngine2 engine)
            : base((uint)enum_EVENTATTRIBUTES.EVENT_SYNCHRONOUS,
                   new Guid("FE5B734C-759D-4E59-AB04-F103343BDD06"))
        {
            _engine = engine;
        }

        public int GetEngine(out IDebugEngine2 pEngine)
        {
            pEngine = _engine;
            return VSConstants.S_OK;
        }
    }

    public class ProgramCreateEvent : DebugEvent, IDebugProgramCreateEvent2
    {
        public ProgramCreateEvent()
            : base((uint)enum_EVENTATTRIBUTES.EVENT_SYNCHRONOUS,
                   new Guid("96CD11EE-ECD4-4E89-957E-B5D496FC4139"))
        {
        }
    }

    public class ProgramDestroyEvent : DebugEvent, IDebugProgramDestroyEvent2
    {
        readonly uint _exitCode = 0;

        public ProgramDestroyEvent(ExitInfo exitInfo)
            : base((uint)enum_EVENTATTRIBUTES.EVENT_SYNCHRONOUS,
                   new Guid("E147E9E3-6440-4073-A7B7-A65592C714B5"))
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

    public class ThreadCreateEvent : DebugEvent, IDebugThreadCreateEvent2
    {
        public ThreadCreateEvent()
            : base((uint)enum_EVENTATTRIBUTES.EVENT_SYNCHRONOUS,
                   new Guid("2090CCFC-70C5-491D-A5E8-BAD2DD9EE3EA"))
        {
        }
    }

    public class ThreadDestroyEvent : DebugEvent, IDebugThreadDestroyEvent2
    {
        readonly uint _exitCode;

        public ThreadDestroyEvent(uint exitCode)
            : base((uint)enum_EVENTATTRIBUTES.EVENT_SYNCHRONOUS,
                   new Guid("2C3B7532-A36F-4A6E-9072-49BE649B8541"))
        {
            _exitCode = exitCode;
        }

        public int GetExitCode(out uint exitCode)
        {
            exitCode = _exitCode;
            return VSConstants.S_OK;
        }
    }

    public class BreakEvent : DebugEvent, IDebugBreakEvent2
    {
        public BreakEvent()
            : base((uint)enum_EVENTATTRIBUTES.EVENT_SYNC_STOP,
                   new Guid("C7405D1D-E24B-44E0-B707-D8A5A4E1641B"))
        {
        }
    }

    public class StepCompleteEvent : DebugEvent, IDebugStepCompleteEvent2
    {
        public StepCompleteEvent()
            : base((uint)enum_EVENTATTRIBUTES.EVENT_SYNC_STOP,
                   new Guid("0F7F24C1-74D9-4EA6-A3EA-7EDB2D81441D"))
        {
        }
    }

    public class BreakpointEvent : DebugEvent, IDebugBreakpointEvent2
    {
        readonly IEnumDebugBoundBreakpoints2 _breakpointEnum;

        public BreakpointEvent(IEnumDebugBoundBreakpoints2 breakpointEnum)
            : base((uint)enum_EVENTATTRIBUTES.EVENT_SYNC_STOP,
                   new Guid("501C1E21-C557-48B8-BA30-A1EAB0BC4A74"))
        {
            _breakpointEnum = breakpointEnum;
        }

        public int EnumBreakpoints(out IEnumDebugBoundBreakpoints2 breakpointEnum)
        {
            breakpointEnum = _breakpointEnum;
            return VSConstants.S_OK;
        }
    }

    public class BreakpointErrorEvent : DebugEvent, IDebugBreakpointErrorEvent2
    {
        readonly DebugBreakpointError _breakpointError;

        public BreakpointErrorEvent(DebugBreakpointError breakpointError)
            : base((uint)enum_EVENTATTRIBUTES.EVENT_SYNCHRONOUS,
                   new Guid("ABB0CA42-F82B-4622-84E4-6903AE90F210"))
        {
            _breakpointError = breakpointError;
        }

        public int GetErrorBreakpoint(out IDebugErrorBreakpoint2 breakpointError)
        {
            breakpointError = _breakpointError;
            return VSConstants.S_OK;
        }
    }

    public class BreakpointBoundEvent : DebugEvent, IDebugBreakpointBoundEvent2
    {
        readonly IDebugPendingBreakpoint2 _pendingBreakpoint;

        public BreakpointBoundEvent(IDebugPendingBreakpoint2 pendingBreakpoint)
            : base((uint)enum_EVENTATTRIBUTES.EVENT_SYNCHRONOUS,
                   new Guid("1dddb704-cf99-4b8a-b746-dabb01dd13a0"))
        {
            _pendingBreakpoint = pendingBreakpoint;
        }

        public int EnumBoundBreakpoints(out IEnumDebugBoundBreakpoints2 boundBreakpointsEnum)
        {
            _pendingBreakpoint.EnumBoundBreakpoints(out boundBreakpointsEnum);
            return VSConstants.S_OK;
        }

        public int GetPendingBreakpoint(out IDebugPendingBreakpoint2 pendingBreakpoint)
        {
            pendingBreakpoint = _pendingBreakpoint;
            return VSConstants.S_OK;
        }
    }

    public class DebugExpressionEvaluationCompleteEvent : DebugEvent,
                                                          IDebugExpressionEvaluationCompleteEvent2
    {
        readonly IDebugExpression2 _expr;
        readonly IDebugProperty2 _result;

        public DebugExpressionEvaluationCompleteEvent(IDebugExpression2 expr,
                                                      IDebugProperty2 result)
            : base((uint)enum_EVENTATTRIBUTES.EVENT_ASYNCHRONOUS,
                   new Guid("C0E13A85-238A-4800-8315-D947C960A843"))
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

    public class ExceptionEvent : DebugEvent, IDebugExceptionEvent2
    {
        readonly string _exceptionName;
        readonly string _detail;
        readonly uint _code;
        readonly enum_EXCEPTION_STATE _state;

        public ExceptionEvent(string exceptionName, uint code, enum_EXCEPTION_STATE state,
                              string description)
            : base((uint)enum_EVENTATTRIBUTES.EVENT_SYNC_STOP, YetiConstants.ExceptionEventGuid)
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
                                     dwState = _state, guidType = Iid };
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

    public class DebugModuleLoadEvent : DebugEvent, IDebugModuleLoadEvent2
    {
        readonly IDebugModule2 _module;
        readonly bool _loaded; // true if a module was loaded, false if a module was unloaded

        public DebugModuleLoadEvent(IDebugModule2 module, bool loaded)
            : base((uint)enum_EVENTATTRIBUTES.EVENT_ASYNCHRONOUS,
                   new Guid("989DB083-0D7C-40D1-A9D9-921BF611A4B2"))
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

    public class DebugSymbolSearchEvent : DebugEvent, IDebugSymbolSearchEvent2
    {
        readonly IDebugModule3 _module;
        readonly string _moduleName;
        readonly string _errorMessage;
        readonly bool _loaded;

        public DebugSymbolSearchEvent(IDebugModule3 module, string moduleName, string errorMessage,
                                      bool loaded)
            : base((uint)enum_EVENTATTRIBUTES.EVENT_ASYNCHRONOUS,
                   new Guid("2A064CA8-D657-4CC8-B11B-F545BFC3FDD3"))
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

    public class DebugEvent : IDebugEvent2
    {
        readonly uint _attributes;

        public Guid Iid { get; private set; }

        protected DebugEvent(uint attributes, Guid iid)
        {
            _attributes = attributes;
            Iid = iid;
        }

        int IDebugEvent2.GetAttributes(out uint attributes)
        {
            attributes = _attributes;
            return VSConstants.S_OK;
        }
    }
}
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

﻿using DebuggerCommonApi;
using LldbApi;
using System;
using System.Collections.Generic;
using System.Text;
using YetiVSI.DebugEngine;

namespace DebuggerGrpcServer
{
    /// <summary>
    /// This class exists to support API extensions SbFrame
    /// </summary>
    public class RemoteFrameImpl : RemoteFrame
    {
        public class Factory
        {
            readonly RemoteValueImpl.Factory _valueFactory;
            readonly ILldbExpressionOptionsFactory _expressionOptionsFactory;
            RemoteThreadImpl.Factory _threadFactory;

            public Factory(RemoteValueImpl.Factory valueFactory,
                ILldbExpressionOptionsFactory expressionOptionsFactory)
            {
                _valueFactory = valueFactory;
                _expressionOptionsFactory = expressionOptionsFactory;
            }

            public void SetRemoteThreadFactory(RemoteThreadImpl.Factory threadFactory)
            {
                this._threadFactory = threadFactory;
            }

            public RemoteFrame Create(SbFrame sbFrame) =>
                sbFrame != null ? new RemoteFrameImpl(
                    sbFrame, _threadFactory, _valueFactory, _expressionOptionsFactory) : null;
        }

        readonly SbFrame _sbFrame;
        readonly RemoteValueImpl.Factory _valueFactory;
        readonly RemoteThreadImpl.Factory _threadFactory;
        readonly ILldbExpressionOptionsFactory _expressionOptionsFactory;

        RemoteFrameImpl(SbFrame sbFrame, RemoteThreadImpl.Factory threadFactory,
            RemoteValueImpl.Factory valueFactory,
            ILldbExpressionOptionsFactory expressionOptionsFactory)
        {
            _sbFrame = sbFrame;
            _threadFactory = threadFactory;
            _valueFactory = valueFactory;
            _expressionOptionsFactory = expressionOptionsFactory;
        }

        public RemoteValue EvaluateExpression(string text)
        {
            SbExpressionOptions options = _expressionOptionsFactory.Create();
            options.SetAutoApplyFixIts(false);
            return _valueFactory.Create(_sbFrame.EvaluateExpression(text, options));
        }

        public RemoteValue EvaluateExpressionLldbEval(string text) =>
            _valueFactory.Create(LldbEval.EvaluateExpression(_sbFrame, text));

        public RemoteValue FindValue(string varName, LldbApi.ValueType value_type) =>
            _valueFactory.Create(_sbFrame.FindValue(varName, value_type));

        public SbFunction GetFunction() => _sbFrame.GetFunction();

        public string GetFunctionName() => _sbFrame.GetFunctionName();

        public SbLineEntry GetLineEntry() => _sbFrame.GetLineEntry();

        public SbModule GetModule() => _sbFrame.GetModule();

        public ulong GetPC() => _sbFrame.GetPC();

        public List<RemoteValue> GetRegisters() =>
            _sbFrame.GetRegisters().ConvertAll(v => _valueFactory.Create(v));

        public SbSymbol GetSymbol() => _sbFrame.GetSymbol();

        public RemoteThread GetThread() => _threadFactory.Create(_sbFrame.GetThread());

        public RemoteValue GetValueForVariablePath(string varPath) =>
            _valueFactory.Create(_sbFrame.GetValueForVariablePath(varPath));

        public List<RemoteValue> GetVariables(bool arguments, bool locals, bool statics,
            bool only_in_scope) =>
            _sbFrame.GetVariables(arguments, locals, statics, only_in_scope).ConvertAll(
                v => _valueFactory.Create(v));

        public AddressRange GetPhysicalStackRange()
        {
            ulong addressMin, addressMax;
            var thread = _sbFrame.GetThread();
            var process = thread.GetProcess();
            var target = process.GetTarget();
            var result = GetFunctionAddressRange(out addressMin, out addressMax, target);
            if (result)
            {
                return new AddressRange(addressMin, addressMax);
            }

            result = GetSymbolAddressRange(out addressMin, out addressMax, target);
            if (result)
            {
                return new AddressRange(addressMin, addressMax);
            }

            result = GetPCAsAddressRange(out addressMin, out addressMax);
            if (result)
            {
                return new AddressRange(addressMin, addressMax);
            }

            return null;
        }

        string GetArguments(FrameInfoFlags fields)
        {
            SbFunction frameFunction = _sbFrame.GetFunction();
            SbTypeList typeList = frameFunction?.GetType()?.GetFunctionArgumentTypes();
            if (typeList == null || typeList.GetSize() == 0)
            {
                return "";
            }

            List<SbValue> valueList = null;
            if ((fields & FrameInfoFlags.FIF_FUNCNAME_ARGS_VALUES) != 0)
            {
                valueList = _sbFrame.GetVariables(true, false, false, false);
            }

            // We should skip the "this" argument. Ideally we would query if
            // a function is a method, but there is no such query. Instead,
            // we literally skip the "this" argument.
            uint argBase = frameFunction.GetArgumentName(0) == "this" ? 1u : 0u;

            var result = new StringBuilder();
            uint argumentCount = typeList.GetSize();
            for (uint i = 0; i < argumentCount; i++)
            {
                if (i > 0)
                {
                    result.Append(", ");
                }
                if ((fields & FrameInfoFlags.FIF_FUNCNAME_ARGS_TYPES) != 0)
                {
                    result.Append(typeList.GetTypeAtIndex(i).GetName());
                }
                if ((fields & FrameInfoFlags.FIF_FUNCNAME_ARGS_NAMES) != 0)
                {
                    // If we are showing types and names, add a space between them.
                    if ((fields & FrameInfoFlags.FIF_FUNCNAME_ARGS_TYPES) != 0)
                    {
                        result.Append(" ");
                    }
                    result.Append(frameFunction.GetArgumentName(argBase + i));
                }
                if ((fields & FrameInfoFlags.FIF_FUNCNAME_ARGS_VALUES) != 0)
                {
                    // In some cases the values are missing (e.g., with incomplete debug info
                    // when doing Debug.ListCallStack in VS's Command Window on crash dump).
                    // Let us handle that case gracefully.
                    int index = (int)(argBase + i);
                    if (index < valueList.Count)
                    {
                        // If we are showing types or names and values, show an equals sign
                        // between them.
                        if ((fields & FrameInfoFlags.FIF_FUNCNAME_ARGS_TYPES) != 0 ||
                            (fields & FrameInfoFlags.FIF_FUNCNAME_ARGS_NAMES) != 0)
                        {
                            result.Append(" = ");
                        }
                        result.Append(valueList[index].GetValue());
                    }
                }
            }
            return result.ToString();
        }

        public FrameInfo<SbModule> GetInfo(FrameInfoFlags fields)
        {
            var info = new FrameInfo<SbModule>();

            var module = _sbFrame.GetModule();

            // We can safely ignore FIF_RETURNTYPE, FIF_ARGS, all FIF_ARGS_*, FIF_ANNOTATEDFRAME,
            // FIF_FILTER_NON_USER_CODE, and FIF_DESIGN_TIME_EXPR_EVAL.
            // For FuncName, we can ignore FIF_FUNCNAME_FORMAT, FIF_FUNCNAME_RETURNTYPE,
            // FIF_FUNCNAME_LANGUAGE, and FIF_FUNCNAME_OFFSET.
            if ((fields & FrameInfoFlags.FIF_FUNCNAME) != 0)
            {
                info.FuncName = "";
                if ((fields & FrameInfoFlags.FIF_FUNCNAME_MODULE) != 0)
                {
                    var platformFileSpec = module?.GetPlatformFileSpec();
                    if (platformFileSpec != null)
                    {
                        info.FuncName = platformFileSpec.GetFilename() + "!";
                    }
                }
                // Strip the leading global scope resolution operator. When viewing parallel stacks
                // visual studio 2017's UI code blows up if the function name starts with ::. Lldb
                // is inconsistent about how function names are produced so it's difficult to tell
                // under which circumstances a function name will include :: ((internal))
                info.FuncName += StripLeadingScopeResolutionOperator(_sbFrame.GetFunctionName());
                if ((fields & FrameInfoFlags.FIF_FUNCNAME_ARGS) != 0)
                {
                    info.FuncName += "(";
                    info.FuncName += GetArguments(fields);
                    info.FuncName += ")";
                }
                if ((fields & FrameInfoFlags.FIF_FUNCNAME_LINES) != 0)
                {
                    var lineEntry = _sbFrame.GetLineEntry();
                    if (lineEntry != null)
                    {
                        uint line = lineEntry.GetLine();
                        if (line != 0)
                        {
                            info.FuncName += " Line " + line;
                        }
                    }
                }
                info.ValidFields |= FrameInfoFlags.FIF_FUNCNAME;
            }
            if ((FrameInfoFlags.FIF_LANGUAGE & fields) != 0)
            {
                var function = _sbFrame.GetFunction();
                LanguageType languageType = LanguageType.UNKNOWN;
                if (function != null)
                {
                    languageType = function.GetLanguage();
                }
                info.Language = SbLanguageRuntime.GetNameFromLanguageType(languageType);
                if (!string.IsNullOrEmpty(info.Language))
                {
                    info.ValidFields |= FrameInfoFlags.FIF_LANGUAGE;
                }
            }

            if ((FrameInfoFlags.FIF_MODULE & fields) != 0)
            {
                string moduleName = _sbFrame.GetModule()?.GetPlatformFileSpec()?.GetFilename();
                if (moduleName != null)
                {
                    info.ValidFields |= FrameInfoFlags.FIF_MODULE;
                    info.ModuleName = moduleName;
                }
            }

            if ((FrameInfoFlags.FIF_STACKRANGE & fields) != 0)
            {
                var stackRange = GetPhysicalStackRange();
                if (stackRange != null)
                {
                    info.AddrMin = stackRange.addressMin;
                    info.AddrMax = stackRange.addressMax;
                    info.ValidFields |= FrameInfoFlags.FIF_STACKRANGE;
                }
            }
            if ((FrameInfoFlags.FIF_FRAME & fields) != 0)
            {
                info.ValidFields |= FrameInfoFlags.FIF_FRAME;
            }
            if ((FrameInfoFlags.FIF_DEBUGINFO & fields) != 0)
            {
                info.HasDebugInfo = Convert.ToInt32(module != null && module.HasCompileUnits());
                info.ValidFields |= FrameInfoFlags.FIF_DEBUGINFO;
            }
            if ((FrameInfoFlags.FIF_STALECODE & fields) != 0)
            {
                info.StaleCode = 0;
                info.ValidFields |= FrameInfoFlags.FIF_STALECODE;
            }
            if ((FrameInfoFlags.FIF_DEBUG_MODULEP & fields) != 0)
            {
                if (module != null)
                {
                    info.Module = module;
                    info.ValidFields |= FrameInfoFlags.FIF_DEBUG_MODULEP;
                }
            }
            return info;
        }

        private bool GetFunctionAddressRange(out ulong addressMin, out ulong addressMax,
            SbTarget target)
        {
            addressMin = 0;
            addressMax = 0;
            var function = _sbFrame.GetFunction();
            if (function == null || target == null)
            {
                return false;
            }

            var startAddress = function.GetStartAddress();
            var endAddress = function.GetEndAddress();
            if (startAddress == null || endAddress == null)
            {
                return false;
            }
            addressMin = startAddress.GetLoadAddress(target);
            addressMax = endAddress.GetLoadAddress(target);
            return true;
        }

        private bool GetSymbolAddressRange(out ulong addressMin, out ulong addressMax,
            SbTarget target)
        {
            addressMin = 0;
            addressMax = 0;
            var symbol = _sbFrame.GetSymbol();
            if (symbol == null || target == null)
            {
                return false;
            }

            var startAddress = symbol.GetStartAddress();
            var endAddress = symbol.GetEndAddress();
            if (startAddress == null || endAddress == null)
            {
                return false;
            }
            addressMin = startAddress.GetLoadAddress(target);
            addressMax = endAddress.GetLoadAddress(target);
            return true;
        }

        private bool GetPCAsAddressRange(out ulong addressMin, out ulong addressMax) {
            addressMin = 0;
            addressMax = 0;
            ulong programCounter = _sbFrame.GetPC();
            if (programCounter != ulong.MaxValue)
            {
                addressMin = programCounter;
                addressMax = programCounter;
                return true;
            }
            return false;
        }

        private string StripLeadingScopeResolutionOperator(string functionName)
        {
            var processed = functionName;
            while (processed.StartsWith("::"))
            {
                processed = processed.Substring(2);
            }
            return processed;
        }
    }
}

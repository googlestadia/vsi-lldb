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
using DebuggerCommonApi;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using YetiCommon.CastleAspects;
using YetiVSI.DebugEngine.AsyncOperations;
using YetiVSI.DebugEngine.CastleAspects;
using YetiVSI.DebugEngine.Interfaces;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.DebugEngine
{
    // TODO: rename to IGgpDebugStackFrame
    public interface IDebugStackFrame : IDebugStackFrame2, IDebugExpressionContext2,
        IDebugStackFrame157, IDecoratorSelf<IDebugStackFrame>
    {
        [InteropBoundary]
        int EnumPropertiesImpl(enum_DEBUGPROP_INFO_FLAGS dwFields, uint nRadix, Guid guidFilter,
                               uint dwTimeout, out uint pcelt, out IEnumDebugPropertyInfo2 ppEnum);

        [InteropBoundary]
        int GetNameWithSignature(out string name);

        RemoteThread Thread { get; }

        RemoteFrame Frame { get; }
    }

    public interface IDebugStackFrameFactory
    {
        IDebugStackFrame Create(AD7FrameInfoCreator ad7FrameInfoCreator, RemoteFrame frame,
                                IDebugThread thread, IDebugEngineHandler debugEngineHandler,
                                IGgpDebugProgram debugProgram);
    }

    public class DebugAsyncStackFrame : SimpleDecoratorSelf<IDebugStackFrame>, IDebugStackFrame
    {
        public class Factory : IDebugStackFrameFactory
        {
            readonly DebugDocumentContext.Factory _debugDocumentContextFactory;
            readonly DebugCodeContext.Factory _debugCodeContextFactory;
            readonly IDebugExpressionFactory _expressionFactory;
            readonly IVariableInformationFactory _varInfoFactory;
            readonly IVariableInformationEnumFactory _varInfoEnumFactory;
            readonly RegisterSetsBuilder.Factory _registerSetsBuilderFactory;
            readonly IChildrenProviderFactory _childrenProviderFactory;
            readonly ITaskExecutor _taskExecutor;

            [Obsolete("This constructor only exists to support mocking libraries.", error: true)]
            protected Factory()
            {
            }

            public Factory(DebugDocumentContext.Factory debugDocumentContextFactory,
                           IChildrenProviderFactory childrenProviderFactory,
                           DebugCodeContext.Factory debugCodeContextFactory,
                           IDebugExpressionFactory expressionFactory,
                           IVariableInformationFactory varInfoFactory,
                           IVariableInformationEnumFactory varInfoEnumFactory,
                           RegisterSetsBuilder.Factory registerSetsBuilderFactory,
                           ITaskExecutor taskExecutor)
            {
                _debugDocumentContextFactory = debugDocumentContextFactory;
                _childrenProviderFactory = childrenProviderFactory;
                _debugCodeContextFactory = debugCodeContextFactory;
                _expressionFactory = expressionFactory;
                _varInfoFactory = varInfoFactory;
                _varInfoEnumFactory = varInfoEnumFactory;
                _registerSetsBuilderFactory = registerSetsBuilderFactory;
                _taskExecutor = taskExecutor;
            }

            public virtual IDebugStackFrame Create(AD7FrameInfoCreator ad7FrameInfoCreator,
                                                   RemoteFrame frame, IDebugThread thread,
                                                   IDebugEngineHandler debugEngineHandler,
                                                   IGgpDebugProgram debugProgram) =>
                new DebugAsyncStackFrame(_debugDocumentContextFactory, _childrenProviderFactory,
                                         _debugCodeContextFactory, _expressionFactory,
                                         _varInfoFactory, _varInfoEnumFactory, ad7FrameInfoCreator,
                                         _registerSetsBuilderFactory.Create(frame),
                                         debugEngineHandler, frame, thread, debugProgram,
                                         _taskExecutor);
        }

        readonly DebugDocumentContext.Factory _debugDocumentContextFactory;
        readonly IChildrenProviderFactory _childrenProviderFactory;
        readonly DebugCodeContext.Factory _debugCodeContextFactory;
        readonly IDebugExpressionFactory _expressionFactory;
        readonly AD7FrameInfoCreator _ad7FrameInfoCreator;
        readonly IVariableInformationFactory _varInfoFactory;
        readonly IVariableInformationEnumFactory _varInfoEnumFactory;
        readonly IRegisterSetsBuilder _registerSetsBuilder;

        readonly IDebugEngineHandler _debugEngineHandler;

        readonly RemoteFrame _lldbFrame;
        readonly IDebugThread _thread;
        readonly IGgpDebugProgram _debugProgram;

        readonly ITaskExecutor _taskExecutor;

        // Cached entities
        readonly Lazy<IDebugDocumentContext2> _documentContext;
        readonly Lazy<IDebugCodeContext2> _codeContext;

        public RemoteThread Thread => _thread.GetRemoteThread();

        public RemoteFrame Frame => _lldbFrame;

        DebugAsyncStackFrame(DebugDocumentContext.Factory debugDocumentContextFactory,
                             IChildrenProviderFactory childrenProviderFactory,
                             DebugCodeContext.Factory debugCodeContextFactory,
                             IDebugExpressionFactory expressionFactory,
                             IVariableInformationFactory varInfoFactory,
                             IVariableInformationEnumFactory varInfoEnumFactory,
                             AD7FrameInfoCreator ad7FrameInfoCreator,
                             IRegisterSetsBuilder registerSetsBuilder,
                             IDebugEngineHandler debugEngineHandler, RemoteFrame frame,
                             IDebugThread thread, IGgpDebugProgram debugProgram,
                             ITaskExecutor taskExecutor)
        {
            _debugDocumentContextFactory = debugDocumentContextFactory;
            _childrenProviderFactory = childrenProviderFactory;
            _debugCodeContextFactory = debugCodeContextFactory;
            _expressionFactory = expressionFactory;
            _varInfoFactory = varInfoFactory;
            _varInfoEnumFactory = varInfoEnumFactory;
            _ad7FrameInfoCreator = ad7FrameInfoCreator;
            _registerSetsBuilder = registerSetsBuilder;
            _debugEngineHandler = debugEngineHandler;
            _debugProgram = debugProgram;
            _lldbFrame = frame;
            _thread = thread;
            _taskExecutor = taskExecutor;

            _documentContext = new Lazy<IDebugDocumentContext2>(CreateDocumentContext);
            _codeContext = new Lazy<IDebugCodeContext2>(CreateCodeContext);
        }

        public int EnumPropertiesImpl(enum_DEBUGPROP_INFO_FLAGS fields, uint radix, Guid guidFilter,
                                      uint timeout, out uint count,
                                      out IEnumDebugPropertyInfo2 propertyEnum)
        {
            var frameVariablesProvider =
                new FrameVariablesProvider(_registerSetsBuilder, _lldbFrame, _varInfoFactory);

            ICollection<IVariableInformation> varInfos = frameVariablesProvider.Get(guidFilter);
            if (varInfos == null)
            {
                count = 0;
                propertyEnum = null;
                return VSConstants.E_NOTIMPL;
            }

            count = (uint) varInfos.Count;

            var childrenAdapter = new ListChildAdapter.Factory().Create(varInfos.ToList());
            var childrenProvider = _childrenProviderFactory.Create(
                _debugProgram.Target, childrenAdapter, fields, radix);

            propertyEnum = _varInfoEnumFactory.Create(childrenProvider);
            return VSConstants.S_OK;
        }

        #region IDebugStackFrame2 functions

        /// <summary>
        /// Workaround to enable Castle interception of EnumProperties().
        /// </summary>
        /// <remarks>
        /// IDebugStackFrame2.EnumProperties() would raise a AccessViolationException if decorated
        /// with a Castle interceptor when running in Visual Studio 2015.  See (internal) for more
        /// info.
        /// </remarks>
        public int EnumProperties(enum_DEBUGPROP_INFO_FLAGS dwFields, uint nRadix,
                                  ref Guid guidFilter, uint dwTimeout, out uint pcelt,
                                  out IEnumDebugPropertyInfo2 ppEnum)
        {
            // Converts |guidFilter| to a non ref type arg so that Castle DynamicProxy doesn't fail
            // to assign values back to it.  See (internal) for more info.
            return Self.EnumPropertiesImpl(dwFields, nRadix, guidFilter, dwTimeout, out pcelt,
                                           out ppEnum);
        }

        public int GetCodeContext(out IDebugCodeContext2 codeContext)
        {
            codeContext = _codeContext.Value;
            return codeContext != null
                ? VSConstants.S_OK
                : VSConstants.E_FAIL;
        }

        public int GetDebugProperty(out IDebugProperty2 property)
        {
            property = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetDocumentContext(out IDebugDocumentContext2 documentContext)
        {
            documentContext = _documentContext.Value;
            return documentContext != null
                ? VSConstants.S_OK
                : VSConstants.E_FAIL;
        }

        public int GetExpressionContext(out IDebugExpressionContext2 expressionContext)
        {
            expressionContext = Self;
            return VSConstants.S_OK;
        }

        // Retrieves requested information about this stack frame.
        // fields specifies what should be included in the output FRAMEINFO.
        public int GetInfo(enum_FRAMEINFO_FLAGS fields, uint radix, FRAMEINFO[] frameInfo)
        {
            var info = _lldbFrame.GetInfo((FrameInfoFlags) fields);
            if (info.HasValue)
            {
                frameInfo[0] = _ad7FrameInfoCreator.Create(Self, info.Value, _debugProgram);
                return VSConstants.S_OK;
            }

            return VSConstants.E_FAIL;
        }

        public int GetLanguageInfo(ref string language, ref Guid languageGuid) =>
            VSConstants.E_NOTIMPL;

        public int GetName(out string name)
        {
            name = _lldbFrame.GetFunctionName();
            return VSConstants.S_OK;
        }

        public int GetNameWithSignature(out string name)
        {
            name = _lldbFrame.GetFunctionNameWithSignature();
            return VSConstants.S_OK;
        }

        public int GetPhysicalStackRange(out ulong addressMin, out ulong addressMax)
        {
            addressMin = 0;
            addressMax = 0;
            var addressRange = _lldbFrame.GetPhysicalStackRange();
            if (addressRange != null)
            {
                addressMin = addressRange.addressMin;
                addressMax = addressRange.addressMax;
                return VSConstants.S_OK;
            }

            return VSConstants.E_FAIL;
        }

        public int GetThread(out IDebugThread2 thread)
        {
            thread = _thread;
            return VSConstants.S_OK;
        }

        #endregion

        #region IDebugExpressionContext2 functions

        public int ParseText(string text, enum_PARSEFLAGS flags, uint radix,
                             out IDebugExpression2 expr, out string strError, out uint error)
        {
            strError = "";
            error = 0;
            expr = _expressionFactory.Create(_lldbFrame, text, _debugEngineHandler, _debugProgram,
                                             _thread);
            return VSConstants.S_OK;
        }

        #endregion

        public int GetPropertyProvider(uint dwFields, uint nRadix, ref Guid guidFilter,
                                       uint dwTimeout,
                                       out IAsyncDebugPropertyInfoProvider ppPropertiesProvider)
        {
            var frameVarProvider = new FrameVariablesProvider(
                _registerSetsBuilder, _lldbFrame, _varInfoFactory);

            // Converts |guidFilter| to a non ref type arg so that Castle DynamicProxy doesn't fail
            // to assign values back to it. Same as for DebugStackFrame.EnumProperties.
            ppPropertiesProvider = new AsyncDebugRootPropertyInfoProvider(
                _debugProgram.Target, frameVarProvider, _taskExecutor, _childrenProviderFactory,
                (enum_DEBUGPROP_INFO_FLAGS) dwFields, nRadix, guidFilter);

            return VSConstants.S_OK;
        }

        IDebugDocumentContext2 CreateDocumentContext()
        {
            LineEntryInfo lineEntry = _lldbFrame.GetLineEntry();
            return lineEntry != null
                ? _debugDocumentContextFactory.Create(lineEntry)
                : null;
        }

        IDebugCodeContext2 CreateCodeContext()
        {
            // Supply the program counter as the code context address.
            return _debugCodeContextFactory.Create(_debugProgram.Target,
                                                   _lldbFrame.GetPC(),
                                                   _lldbFrame.GetFunctionNameWithSignature(),
                                                   _documentContext.Value);
        }
    }
}
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
    public delegate IDebugStackFrame CreateDebugStackFrameDelegate(
        AD7FrameInfoCreator ad7FrameInfoCreator, RemoteFrame frame, IDebugThread thread,
        IDebugEngineHandler debugEngineHandler, IGgpDebugProgram debugProgram);

    public interface IDebugStackFrame : IDebugStackFrame2, IDebugExpressionContext2,
        IDecoratorSelf<IDebugStackFrame>
    {
        [InteropBoundary]
        int EnumPropertiesImpl(enum_DEBUGPROP_INFO_FLAGS dwFields, uint nRadix, Guid guidFilter,
                               uint dwTimeout, out uint pcelt, out IEnumDebugPropertyInfo2 ppEnum);

        [InteropBoundary]
        int GetPropertyProviderImpl(uint dwFields, uint nRadix, Guid guidFilter, uint dwTimeout,
                                    out IAsyncDebugPropertyInfoProvider ppPropertiesProvider);

        [InteropBoundary]
        int GetNameWithSignature(out string name);
    }

    public interface IDebugStackFrameAsync : IDebugStackFrame, IDebugStackFrame157
    {
    }

    public abstract class BaseDebugStackFrame : SimpleDecoratorSelf<IDebugStackFrame>
    {
        readonly DebugDocumentContext.Factory debugDocumentContextFactory;
        readonly IChildrenProviderFactory _childrenProviderFactory;
        readonly DebugCodeContext.Factory debugCodeContextFactory;
        readonly CreateDebugExpressionDelegate createExpressionDelegate;
        AD7FrameInfoCreator ad7FrameInfoCreator;
        readonly IVariableInformationFactory varInfoFactory;
        readonly IVariableInformationEnumFactory varInfoEnumFactory;
        readonly IRegisterSetsBuilder registerSetsBuilder;

        readonly IDebugEngineHandler debugEngineHandler;

        readonly RemoteFrame lldbFrame;
        readonly IDebugThread2 thread;
        readonly IGgpDebugProgram debugProgram;

        readonly ITaskExecutor _taskExecutor;

        // Cached entities
        Lazy<IDebugDocumentContext2> documentContext;
        Lazy<IDebugCodeContext2> codeContext;

        protected BaseDebugStackFrame(DebugDocumentContext.Factory debugDocumentContextFactory,
                                      IChildrenProviderFactory childrenProviderFactory,
                                      DebugCodeContext.Factory debugCodeContextFactory,
                                      CreateDebugExpressionDelegate createExpressionDelegate,
                                      IVariableInformationFactory varInfoFactory,
                                      IVariableInformationEnumFactory varInfoEnumFactory,
                                      AD7FrameInfoCreator ad7FrameInfoCreator,
                                      IRegisterSetsBuilder registerSetsBuilder,
                                      IDebugEngineHandler debugEngineHandler, RemoteFrame frame,
                                      IDebugThread2 thread, IGgpDebugProgram debugProgram,
                                      ITaskExecutor taskExecutor)
        {
            this.debugDocumentContextFactory = debugDocumentContextFactory;
            this._childrenProviderFactory = childrenProviderFactory;
            this.debugCodeContextFactory = debugCodeContextFactory;
            this.createExpressionDelegate = createExpressionDelegate;
            this.varInfoFactory = varInfoFactory;
            this.varInfoEnumFactory = varInfoEnumFactory;
            this.ad7FrameInfoCreator = ad7FrameInfoCreator;
            this.registerSetsBuilder = registerSetsBuilder;
            this.debugEngineHandler = debugEngineHandler;
            this.debugProgram = debugProgram;
            lldbFrame = frame;
            this.thread = thread;
            this._taskExecutor = taskExecutor;

            documentContext = new Lazy<IDebugDocumentContext2>(() => CreateDocumentContext());
            codeContext = new Lazy<IDebugCodeContext2>(() => CreateCodeContext());
        }

        public int EnumPropertiesImpl(enum_DEBUGPROP_INFO_FLAGS fields, uint radix, Guid guidFilter,
                                      uint timeout, out uint count,
                                      out IEnumDebugPropertyInfo2 propertyEnum)
        {
            var frameVariablesProvider = new FrameVariablesProvider(registerSetsBuilder, lldbFrame,
                                                                    varInfoFactory);

            ICollection<IVariableInformation> varInfos = frameVariablesProvider.Get(guidFilter);
            if (varInfos == null)
            {
                count = 0;
                propertyEnum = null;
                return VSConstants.E_NOTIMPL;
            }

            count = (uint) varInfos.Count;
            var childrenAdapter = new ListChildAdapter.Factory().Create(varInfos.ToList());
            var childrenProvider = _childrenProviderFactory.Create(childrenAdapter, fields, radix);

            propertyEnum = varInfoEnumFactory.Create(childrenProvider);
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
            codeContext = this.codeContext.Value;
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
            documentContext = this.documentContext.Value;
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
            var info = lldbFrame.GetInfo((FrameInfoFlags) fields);
            if (info.HasValue)
            {
                frameInfo[0] = ad7FrameInfoCreator.Create(Self, info.Value, debugProgram);
                return VSConstants.S_OK;
            }

            return VSConstants.E_FAIL;
        }

        public int GetLanguageInfo(ref string language, ref Guid languageGuid)
        {
            return VSConstants.E_NOTIMPL;
        }

        public int GetName(out string name)
        {
            name = lldbFrame.GetFunctionName();
            return VSConstants.S_OK;
        }

        public int GetNameWithSignature(out string name)
        {
            name = lldbFrame.GetFunctionNameWithSignature();
            return VSConstants.S_OK;
        }

        public int GetPhysicalStackRange(out ulong addressMin, out ulong addressMax)
        {
            addressMin = 0;
            addressMax = 0;
            var addressRange = lldbFrame.GetPhysicalStackRange();
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
            thread = this.thread;
            return VSConstants.S_OK;
        }

        #endregion

        #region IDebugExpressionContext2 functions

        public int ParseText(string text, enum_PARSEFLAGS flags, uint radix,
                             out IDebugExpression2 expr, out string strError, out uint error)
        {
            strError = "";
            error = 0;
            expr = createExpressionDelegate.Invoke(lldbFrame, text, debugEngineHandler,
                                                   debugProgram, thread);
            return VSConstants.S_OK;
        }

        #endregion

        public int GetPropertyProviderImpl(uint dwFields, uint nRadix, Guid guidFilter,
                                           uint dwTimeout,
                                           out IAsyncDebugPropertyInfoProvider ppPropertiesProvider)
        {
            ppPropertiesProvider = new AsyncDebugRootPropertyInfoProvider(
                new FrameVariablesProvider(registerSetsBuilder, lldbFrame, varInfoFactory),
                _taskExecutor, _childrenProviderFactory, (enum_DEBUGPROP_INFO_FLAGS) dwFields,
                nRadix, guidFilter);

            return VSConstants.S_OK;
        }

        IDebugDocumentContext2 CreateDocumentContext()
        {
            LineEntryInfo lineEntry = lldbFrame.GetLineEntry();
            return lineEntry != null
                ? debugDocumentContextFactory.Create(lineEntry)
                : null;
        }

        IDebugCodeContext2 CreateCodeContext()
        {
            // Supply the program counter as the code context address.
            return debugCodeContextFactory.Create(lldbFrame.GetPC(),
                                                  lldbFrame.GetFunctionNameWithSignature(),
                                                  documentContext.Value, Guid.Empty);
        }
    }

    public class DebugStackFrame : BaseDebugStackFrame, IDebugStackFrame
    {
        public class Factory
        {
            readonly DebugDocumentContext.Factory debugDocumentContextFactory;
            readonly DebugCodeContext.Factory debugCodeContextFactory;
            readonly CreateDebugExpressionDelegate createExpressionDelegate;
            readonly IVariableInformationFactory varInfoFactory;
            readonly IVariableInformationEnumFactory varInfoEnumFactory;
            readonly RegisterSetsBuilder.Factory registerSetsBuilderFactory;
            readonly IChildrenProviderFactory childrenProviderFactory;
            readonly ITaskExecutor taskExecutor;

            [Obsolete("This constructor only exists to support mocking libraries.", error: true)]
            protected Factory()
            {
            }

            public Factory(DebugDocumentContext.Factory debugDocumentContextFactory,
                           IChildrenProviderFactory childrenProviderFactory,
                           DebugCodeContext.Factory debugCodeContextFactory,
                           CreateDebugExpressionDelegate createExpressionDelegate,
                           IVariableInformationFactory varInfoFactory,
                           IVariableInformationEnumFactory varInfoEnumFactory,
                           RegisterSetsBuilder.Factory registerSetsBuilderFactory,
                           ITaskExecutor taskExecutor)
            {
                this.debugDocumentContextFactory = debugDocumentContextFactory;
                this.childrenProviderFactory = childrenProviderFactory;
                this.debugCodeContextFactory = debugCodeContextFactory;
                this.createExpressionDelegate = createExpressionDelegate;
                this.varInfoFactory = varInfoFactory;
                this.varInfoEnumFactory = varInfoEnumFactory;
                this.registerSetsBuilderFactory = registerSetsBuilderFactory;
                this.taskExecutor = taskExecutor;
            }

            public virtual IDebugStackFrame Create(AD7FrameInfoCreator ad7FrameInfoCreator,
                                                   RemoteFrame frame, IDebugThread thread,
                                                   IDebugEngineHandler debugEngineHandler,
                                                   IGgpDebugProgram debugProgram) =>
                new DebugStackFrame(debugDocumentContextFactory, childrenProviderFactory,
                                    debugCodeContextFactory, createExpressionDelegate,
                                    varInfoFactory, varInfoEnumFactory, ad7FrameInfoCreator,
                                    registerSetsBuilderFactory.Create(frame), debugEngineHandler,
                                    frame, thread, debugProgram, taskExecutor);
        }

        public DebugStackFrame(DebugDocumentContext.Factory debugDocumentContextFactory,
                               IChildrenProviderFactory childrenProviderFactory,
                               DebugCodeContext.Factory debugCodeContextFactory,
                               CreateDebugExpressionDelegate createExpressionDelegate,
                               IVariableInformationFactory varInfoFactory,
                               IVariableInformationEnumFactory varInfoEnumFactory,
                               AD7FrameInfoCreator ad7FrameInfoCreator,
                               IRegisterSetsBuilder registerSetsBuilder,
                               IDebugEngineHandler debugEngineHandler, RemoteFrame frame,
                               IDebugThread2 thread, IGgpDebugProgram debugProgram,
                               ITaskExecutor taskExecutor) : base(debugDocumentContextFactory,
                                                                  childrenProviderFactory,
                                                                  debugCodeContextFactory,
                                                                  createExpressionDelegate,
                                                                  varInfoFactory,
                                                                  varInfoEnumFactory,
                                                                  ad7FrameInfoCreator,
                                                                  registerSetsBuilder,
                                                                  debugEngineHandler, frame, thread,
                                                                  debugProgram, taskExecutor)
        {
        }
    }

    public class DebugStackFrameAsync : BaseDebugStackFrame, IDebugStackFrameAsync
    {
        public class Factory
        {
            readonly DebugDocumentContext.Factory debugDocumentContextFactory;
            readonly DebugCodeContext.Factory debugCodeContextFactory;
            readonly CreateDebugExpressionDelegate createExpressionDelegate;
            readonly IVariableInformationFactory varInfoFactory;
            readonly IVariableInformationEnumFactory varInfoEnumFactory;
            readonly RegisterSetsBuilder.Factory registerSetsBuilderFactory;
            readonly IChildrenProviderFactory childrenProviderFactory;
            readonly ITaskExecutor taskExecutor;

            [Obsolete("This constructor only exists to support mocking libraries.", error: true)]
            protected Factory()
            {
            }

            public Factory(DebugDocumentContext.Factory debugDocumentContextFactory,
                           IChildrenProviderFactory childrenProviderFactory,
                           DebugCodeContext.Factory debugCodeContextFactory,
                           CreateDebugExpressionDelegate createExpressionDelegate,
                           IVariableInformationFactory varInfoFactory,
                           IVariableInformationEnumFactory varInfoEnumFactory,
                           RegisterSetsBuilder.Factory registerSetsBuilderFactory,
                           ITaskExecutor taskExecutor)
            {
                this.debugDocumentContextFactory = debugDocumentContextFactory;
                this.childrenProviderFactory = childrenProviderFactory;
                this.debugCodeContextFactory = debugCodeContextFactory;
                this.createExpressionDelegate = createExpressionDelegate;
                this.varInfoFactory = varInfoFactory;
                this.varInfoEnumFactory = varInfoEnumFactory;
                this.registerSetsBuilderFactory = registerSetsBuilderFactory;
                this.taskExecutor = taskExecutor;
            }

            public virtual IDebugStackFrameAsync Create(AD7FrameInfoCreator ad7FrameInfoCreator,
                                                        RemoteFrame frame, IDebugThread thread,
                                                        IDebugEngineHandler debugEngineHandler,
                                                        IGgpDebugProgram debugProgram) =>
                new DebugStackFrameAsync(debugDocumentContextFactory, childrenProviderFactory,
                                         debugCodeContextFactory, createExpressionDelegate,
                                         varInfoFactory, varInfoEnumFactory, ad7FrameInfoCreator,
                                         registerSetsBuilderFactory.Create(frame),
                                         debugEngineHandler, frame, thread, debugProgram,
                                         taskExecutor);
        }

        DebugStackFrameAsync(DebugDocumentContext.Factory debugDocumentContextFactory,
                             IChildrenProviderFactory childrenProviderFactory,
                             DebugCodeContext.Factory debugCodeContextFactory,
                             CreateDebugExpressionDelegate createExpressionDelegate,
                             IVariableInformationFactory varInfoFactory,
                             IVariableInformationEnumFactory varInfoEnumFactory,
                             AD7FrameInfoCreator ad7FrameInfoCreator,
                             IRegisterSetsBuilder registerSetsBuilder,
                             IDebugEngineHandler debugEngineHandler, RemoteFrame frame,
                             IDebugThread2 thread, IGgpDebugProgram debugProgram,
                             ITaskExecutor taskExecutor) : base(debugDocumentContextFactory,
                                                                childrenProviderFactory,
                                                                debugCodeContextFactory,
                                                                createExpressionDelegate,
                                                                varInfoFactory, varInfoEnumFactory,
                                                                ad7FrameInfoCreator,
                                                                registerSetsBuilder,
                                                                debugEngineHandler, frame, thread,
                                                                debugProgram, taskExecutor)
        {
        }

        public int GetPropertyProvider(uint dwFields, uint nRadix, ref Guid guidFilter,
                                       uint dwTimeout,
                                       out IAsyncDebugPropertyInfoProvider ppPropertiesProvider)
        {
            // Converts |guidFilter| to a non ref type arg so that Castle DynamicProxy doesn't fail
            // to assign values back to it. Same as for DebugStackFrame.EnumProperties.
            return Self.GetPropertyProviderImpl(dwFields, nRadix, guidFilter, dwTimeout,
                                                out ppPropertiesProvider);
        }
    }
}
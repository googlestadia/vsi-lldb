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
using NUnit.Framework;
using NSubstitute;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using YetiVSI.DebugEngine;
using System.Collections.Generic;
using YetiVSI.DebugEngine.Variables;
using System;
using DebuggerCommonApi;
using Microsoft.VisualStudio.Threading;
using YetiVSI.DebugEngine.AsyncOperations;
using YetiVSI.DebugEngine.Interfaces;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class DebugStackFrameTests
    {
        const ulong TEST_PC = 0x123456789abcdef0;
        const string NAME = "DebugStackFrameTests";

        IDebugStackFrame stackFrame;
        IDebugStackFrameAsync stackFrameAsync;
        RemoteFrame mockDebuggerStackFrame;
        LineEntryInfo lineEntry;
        IDebugDocumentContext2 mockDocumentContext;
        IDebugThread mockThread;
        DebugDocumentContext.Factory mockDocumentContextFactory;
        DebugCodeContext.Factory mockCodeContextFactory;
        DebugExpression.Factory mockExpressionFactory;
        IDebugModuleCache mockModuleCache;
        IDebugEngineHandler mockDebugEngineHandler;
        ITaskExecutor taskExecutor;
        IGgpDebugProgram mockProgram;

        [SetUp]
        public void SetUp()
        {
            lineEntry = new LineEntryInfo();
            mockDocumentContext = Substitute.For<IDebugDocumentContext2>();
            mockThread = Substitute.For<IDebugThread>();
            mockDocumentContextFactory = Substitute.For<DebugDocumentContext.Factory>();
            mockDocumentContextFactory.Create(lineEntry).Returns(mockDocumentContext);
            mockDebuggerStackFrame = Substitute.For<RemoteFrame>();
            mockDebuggerStackFrame.GetLineEntry().Returns(lineEntry);
            mockDebuggerStackFrame.GetPC().Returns(TEST_PC);
            mockDebuggerStackFrame.GetFunctionName().Returns(NAME);
            mockDebuggerStackFrame.GetFunctionNameWithSignature().Returns(NAME);

            mockCodeContextFactory = Substitute.For<DebugCodeContext.Factory>();
            mockExpressionFactory = Substitute.For<DebugExpression.Factory>();
            mockModuleCache = Substitute.For<IDebugModuleCache>();

            mockDebugEngineHandler = Substitute.For<IDebugEngineHandler>();
            mockProgram = Substitute.For<IGgpDebugProgram>();

            taskExecutor = new TaskExecutor(new JoinableTaskContext().Factory);

            var childAdapterFactory = new RemoteValueChildAdapter.Factory();
            var varInfoFactory = new LLDBVariableInformationFactory(childAdapterFactory);
            var varInfoEnumFactory = new VariableInformationEnum.Factory(taskExecutor);
            var childrenProviderFactory = new ChildrenProvider.Factory();
            var propertyFactory =
                new DebugProperty.Factory(varInfoEnumFactory, childrenProviderFactory,
                                          Substitute.For<DebugCodeContext.Factory>(),
                                          new VsExpressionCreator(), taskExecutor);
            childrenProviderFactory.Initialize(propertyFactory.Create);
            var registerSetsBuilderFactory = new RegisterSetsBuilder.Factory(varInfoFactory);

            stackFrame = new DebugStackFrame.Factory(mockDocumentContextFactory,
                                                     childrenProviderFactory, mockCodeContextFactory, mockExpressionFactory.Create,
                                                     varInfoFactory, varInfoEnumFactory, registerSetsBuilderFactory, taskExecutor)
                .Create(new AD7FrameInfoCreator(mockModuleCache), mockDebuggerStackFrame,
                    mockThread, mockDebugEngineHandler, mockProgram);

            stackFrameAsync = new DebugStackFrameAsync.Factory(mockDocumentContextFactory,
                    childrenProviderFactory, mockCodeContextFactory, mockExpressionFactory.Create,
                    varInfoFactory, varInfoEnumFactory, registerSetsBuilderFactory, taskExecutor)
                .Create(new AD7FrameInfoCreator(mockModuleCache), mockDebuggerStackFrame,
                    mockThread, mockDebugEngineHandler, mockProgram);
        }

        [Test]
        public void GetCodeContext()
        {
            var mockCodeContext = Substitute.For<IDebugCodeContext2>();
            mockCodeContextFactory
                .Create(TEST_PC, NAME, mockDocumentContext, Guid.Empty)
                .Returns(mockCodeContext);

            Assert.AreEqual(VSConstants.S_OK,
                            stackFrame.GetCodeContext(out IDebugCodeContext2 codeContext));
            Assert.AreEqual(codeContext, mockCodeContext);
        }

        [Test]
        public void GetCodeContextNoDocumentContext()
        {
            var mockCodeContext = Substitute.For<IDebugCodeContext2>();
            mockCodeContextFactory
                .Create(TEST_PC, NAME, null, Guid.Empty)
                .Returns(mockCodeContext);
            LineEntryInfo lineEntryNull = null;
            mockDebuggerStackFrame.GetLineEntry().Returns(lineEntryNull);

            IDebugCodeContext2 codeContext;
            Assert.AreEqual(VSConstants.S_OK, stackFrame.GetCodeContext(out codeContext));
            Assert.AreEqual(codeContext, mockCodeContext);
        }

        [Test]
        public void GetDocumentContext()
        {
            IDebugDocumentContext2 documentContext;
            Assert.AreEqual(VSConstants.S_OK,
                stackFrame.GetDocumentContext(out documentContext));
            Assert.AreEqual(mockDocumentContext, documentContext);
        }

        [Test]
        public void GetDocumentContextNoLineEntry()
        {
            LineEntryInfo lineEntryNull = null;
            mockDebuggerStackFrame.GetLineEntry().Returns(lineEntryNull);
            IDebugDocumentContext2 documentContext;
            Assert.AreEqual(VSConstants.E_FAIL,
                stackFrame.GetDocumentContext(out documentContext));
            Assert.AreEqual(null, documentContext);
        }

        [Test]
        public void GetExpressionContext()
        {
            IDebugExpressionContext2 context;
            Assert.AreEqual(VSConstants.S_OK, stackFrame.GetExpressionContext(out context));
            Assert.AreEqual(stackFrame, context);
        }

        [Test]
        public void ParseText()
        {
            var test_expression = "test expression";

            var mockExpression = Substitute.For<IDebugExpression>();
            mockExpressionFactory.Create(mockDebuggerStackFrame, test_expression,
                mockDebugEngineHandler, mockProgram, mockThread)
                .Returns(mockExpression);

            IDebugExpression2 expression;
            string errorString;
            uint error;
            Assert.AreEqual(VSConstants.S_OK,
                stackFrame.ParseText(test_expression, 0, 0, out expression, out errorString,
                out error));
            Assert.AreEqual(mockExpression, expression);
            Assert.AreEqual(0, error);
            Assert.AreEqual("", errorString);
        }

        [Test]
        public void EnumPropertiesSupportedFilter()
        {
            mockDebuggerStackFrame.GetVariables(false, true, false, true).Returns(
                new List<RemoteValue> { });

            IEnumDebugPropertyInfo2 propertyEnum;
            uint count;
            var guidFilter = FrameVariablesProvider.PropertyFilterGuids.AllLocals;
            Assert.AreEqual(VSConstants.S_OK, stackFrame.EnumProperties(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME, 0, ref guidFilter, 0, out count,
                out propertyEnum));
        }

        [Test]
        public void EnumPropertiesUnsupportedFilter()
        {
            IEnumDebugPropertyInfo2 propertyEnum;
            uint count;
            var guidFilter = new Guid("12345678-1234-1234-1234-123456789123");
            Assert.AreEqual(VSConstants.E_NOTIMPL, stackFrame.EnumProperties(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME, 0, ref guidFilter, 0, out count,
                out propertyEnum));
        }

        [Test]
        public void GetThread()
        {
            IDebugThread2 thread;
            Assert.AreEqual(VSConstants.S_OK, stackFrame.GetThread(out thread));
            Assert.AreEqual(mockThread, thread);
        }

        [Test]
        public void GetFrame()
        {
            mockDebuggerStackFrame.GetInfo(FrameInfoFlags.FIF_FRAME).Returns(
                new FrameInfo<SbModule> { ValidFields = FrameInfoFlags.FIF_FRAME });

            var info = new FRAMEINFO[1];
            var fields = enum_FRAMEINFO_FLAGS.FIF_FRAME;

            Assert.AreEqual(VSConstants.S_OK, stackFrame.GetInfo(fields, 0, info));
            Assert.AreEqual(enum_FRAMEINFO_FLAGS.FIF_FRAME,
                info[0].m_dwValidFields & enum_FRAMEINFO_FLAGS.FIF_FRAME);
            Assert.AreEqual(stackFrame, info[0].m_pFrame);
        }

        [Test]
        public void GetModule()
        {
            var mockModule = Substitute.For<SbModule>();
            mockDebuggerStackFrame.GetInfo(FrameInfoFlags.FIF_DEBUG_MODULEP).Returns(
                new FrameInfo<SbModule>
                {
                    ValidFields = FrameInfoFlags.FIF_DEBUG_MODULEP,
                    Module = mockModule
                });
            var debugModule = Substitute.For<IDebugModule3>();
            mockModuleCache.GetOrCreate(mockModule, mockProgram).Returns(debugModule);

            var info = new FRAMEINFO[1];
            var fields = enum_FRAMEINFO_FLAGS.FIF_DEBUG_MODULEP;

            Assert.AreEqual(VSConstants.S_OK, stackFrame.GetInfo(fields, 0, info));
            Assert.AreEqual(enum_FRAMEINFO_FLAGS.FIF_DEBUG_MODULEP,
                            info[0].m_dwValidFields & enum_FRAMEINFO_FLAGS.FIF_DEBUG_MODULEP);
            Assert.AreEqual(debugModule, info[0].m_pModule);
        }

        [Test]
        public void EnsureFrameInfoFlagsMatchAD7()
        {
            CollectionAssert.AreEqual(
                Enum.GetNames(typeof(enum_FRAMEINFO_FLAGS)),
                Enum.GetNames(typeof(FrameInfoFlags)));

            Assert.AreEqual(
                Enum.GetUnderlyingType(typeof(enum_FRAMEINFO_FLAGS)),
                Enum.GetUnderlyingType(typeof(FrameInfoFlags)));

            var underlyingType = Enum.GetUnderlyingType(typeof(FrameInfoFlags));
            var ad7Values = Enum.GetValues(typeof(enum_FRAMEINFO_FLAGS));
            var customValues = Enum.GetValues(typeof(FrameInfoFlags));
            for (int i = 0; i < ad7Values.Length; ++i)
            {
                Assert.AreEqual(
                    Convert.ChangeType(ad7Values.GetValue(i), underlyingType),
                    Convert.ChangeType(customValues.GetValue(i), underlyingType));
            }
        }

        [Test]
        public void GetPropertyProvider()
        {
            Guid guidFilter = FrameVariablesProvider.PropertyFilterGuids.AllLocals;

            int status = stackFrameAsync.GetPropertyProvider(
                (uint) enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME, 0, ref guidFilter, 0,
                out IAsyncDebugPropertyInfoProvider provider);

            Assert.AreEqual(VSConstants.S_OK, status);
            Assert.IsNotNull(provider);
            Assert.IsInstanceOf<AsyncDebugRootPropertyInfoProvider>(provider);
        }
    }
}
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
        const ulong _testPc = 0x123456789abcdef0;
        const string _name = "DebugStackFrameTests";

        RemoteTarget _mockTarget;
        IDebugStackFrame _stackFrame;
        RemoteFrame _mockDebuggerStackFrame;
        LineEntryInfo _lineEntry;
        IDebugDocumentContext2 _mockDocumentContext;
        IDebugThread _mockThread;
        DebugDocumentContext.Factory _mockDocumentContextFactory;
        DebugCodeContext.Factory _mockCodeContextFactory;
        IDebugExpressionFactory _mockExpressionFactory;
        IDebugModuleCache _mockModuleCache;
        IDebugEngineHandler _mockDebugEngineHandler;
        ITaskExecutor _taskExecutor;
        IGgpDebugProgram _mockProgram;

        [SetUp]
        public void SetUp()
        {
            _lineEntry = new LineEntryInfo();
            _mockDocumentContext = Substitute.For<IDebugDocumentContext2>();
            _mockThread = Substitute.For<IDebugThread>();
            _mockDocumentContextFactory = Substitute.For<DebugDocumentContext.Factory>();
            _mockDocumentContextFactory.Create(_lineEntry).Returns(_mockDocumentContext);
            _mockDebuggerStackFrame = Substitute.For<RemoteFrame>();
            _mockDebuggerStackFrame.GetLineEntry().Returns(_lineEntry);
            _mockDebuggerStackFrame.GetPC().Returns(_testPc);
            _mockDebuggerStackFrame.GetFunctionName().Returns(_name);
            _mockDebuggerStackFrame.GetFunctionNameWithSignature().Returns(_name);

            _mockCodeContextFactory = Substitute.For<DebugCodeContext.Factory>();
            _mockExpressionFactory = Substitute.For<IDebugExpressionFactory>();
            _mockModuleCache = Substitute.For<IDebugModuleCache>();

            _mockDebugEngineHandler = Substitute.For<IDebugEngineHandler>();
            _mockProgram = Substitute.For<IGgpDebugProgram>();
            _mockTarget = Substitute.For<RemoteTarget>();
            _mockProgram.Target.Returns(_mockTarget);

            _taskExecutor = new TaskExecutor(new JoinableTaskContext().Factory);

            var childAdapterFactory = new RemoteValueChildAdapter.Factory();
            var varInfoFactory = new LLDBVariableInformationFactory(childAdapterFactory);
            var varInfoEnumFactory = new VariableInformationEnum.Factory(_taskExecutor);
            var childrenProviderFactory = new ChildrenProvider.Factory();
            var propertyFactory = new DebugAsyncProperty.Factory(
                varInfoEnumFactory, childrenProviderFactory,
                Substitute.For<DebugCodeContext.Factory>(), new VsExpressionCreator(),
                _taskExecutor);
            childrenProviderFactory.Initialize(propertyFactory);
            var registerSetsBuilderFactory = new RegisterSetsBuilder.Factory(varInfoFactory);

            _stackFrame = new DebugAsyncStackFrame.Factory(_mockDocumentContextFactory,
                                                           childrenProviderFactory,
                                                           _mockCodeContextFactory,
                                                           _mockExpressionFactory, varInfoFactory,
                                                           varInfoEnumFactory,
                                                           registerSetsBuilderFactory,
                                                           _taskExecutor)
                .Create(new AD7FrameInfoCreator(_mockModuleCache), _mockDebuggerStackFrame,
                        _mockThread,
                        _mockDebugEngineHandler, _mockProgram);
        }

        [Test]
        public void GetCodeContext()
        {
            var mockCodeContext = Substitute.For<IGgpDebugCodeContext>();
            _mockCodeContextFactory.Create(_mockTarget, _testPc, _name, _mockDocumentContext)
                .Returns(mockCodeContext);

            Assert.AreEqual(VSConstants.S_OK,
                            _stackFrame.GetCodeContext(out IDebugCodeContext2 codeContext));
            Assert.AreEqual(codeContext, mockCodeContext);
        }

        [Test]
        public void GetCodeContextNoDocumentContext()
        {
            var mockCodeContext = Substitute.For<IGgpDebugCodeContext>();
            _mockCodeContextFactory.Create(_mockTarget, _testPc, _name, null)
                .Returns(mockCodeContext);
            LineEntryInfo lineEntryNull = null;
            _mockDebuggerStackFrame.GetLineEntry().Returns(lineEntryNull);

            Assert.AreEqual(VSConstants.S_OK,
                            _stackFrame.GetCodeContext(out IDebugCodeContext2 codeContext));
            Assert.AreEqual(codeContext, mockCodeContext);
        }

        [Test]
        public void GetDocumentContext()
        {
            Assert.AreEqual(VSConstants.S_OK,
                            _stackFrame.GetDocumentContext(
                                out IDebugDocumentContext2 documentContext));
            Assert.AreEqual(_mockDocumentContext, documentContext);
        }

        [Test]
        public void GetDocumentContextNoLineEntry()
        {
            LineEntryInfo lineEntryNull = null;
            _mockDebuggerStackFrame.GetLineEntry().Returns(lineEntryNull);
            Assert.AreEqual(VSConstants.E_FAIL,
                            _stackFrame.GetDocumentContext(
                                out IDebugDocumentContext2 documentContext));
            Assert.AreEqual(null, documentContext);
        }

        [Test]
        public void GetExpressionContext()
        {
            Assert.AreEqual(VSConstants.S_OK,
                            _stackFrame.GetExpressionContext(out IDebugExpressionContext2 context));
            Assert.AreEqual(_stackFrame, context);
        }

        [Test]
        public void ParseText()
        {
            var testExpression = "test expression";

            var mockExpression = Substitute.For<IDebugExpression>();
            _mockExpressionFactory.Create(_mockDebuggerStackFrame, testExpression,
                                          _mockDebugEngineHandler, _mockProgram, _mockThread)
                .Returns(mockExpression);

            Assert.AreEqual(VSConstants.S_OK,
                            _stackFrame.ParseText(testExpression, 0, 0,
                                                  out IDebugExpression2 expression,
                                                  out string errorString, out uint error));
            Assert.AreEqual(mockExpression, expression);
            Assert.AreEqual(0, error);
            Assert.AreEqual("", errorString);
        }

        [Test]
        public void EnumPropertiesSupportedFilter()
        {
            _mockDebuggerStackFrame.GetVariables(false, true, false, true)
                .Returns(new List<RemoteValue> { });

            var guidFilter = FrameVariablesProvider.PropertyFilterGuids.AllLocals;
            Assert.AreEqual(VSConstants.S_OK,
                            _stackFrame.EnumProperties(
                                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME, 0, ref guidFilter, 0,
                                out uint count, out IEnumDebugPropertyInfo2 propertyEnum));
        }

        [Test]
        public void EnumPropertiesUnsupportedFilter()
        {
            var guidFilter = new Guid("12345678-1234-1234-1234-123456789123");
            Assert.AreEqual(VSConstants.E_NOTIMPL,
                            _stackFrame.EnumProperties(
                                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME, 0, ref guidFilter, 0,
                                out uint count, out IEnumDebugPropertyInfo2 propertyEnum));
        }

        [Test]
        public void GetThread()
        {
            Assert.AreEqual(VSConstants.S_OK, _stackFrame.GetThread(out IDebugThread2 thread));
            Assert.AreEqual(_mockThread, thread);
        }

        [Test]
        public void GetFrame()
        {
            _mockDebuggerStackFrame.GetInfo(FrameInfoFlags.FIF_FRAME).Returns(
                new FrameInfo<SbModule> { ValidFields = FrameInfoFlags.FIF_FRAME });

            var info = new FRAMEINFO[1];
            var fields = enum_FRAMEINFO_FLAGS.FIF_FRAME;

            Assert.AreEqual(VSConstants.S_OK, _stackFrame.GetInfo(fields, 0, info));
            Assert.AreEqual(enum_FRAMEINFO_FLAGS.FIF_FRAME,
                            info[0].m_dwValidFields & enum_FRAMEINFO_FLAGS.FIF_FRAME);
            Assert.AreEqual(_stackFrame, info[0].m_pFrame);
        }

        [Test]
        public void GetModule()
        {
            var mockModule = Substitute.For<SbModule>();
            _mockDebuggerStackFrame.GetInfo(FrameInfoFlags.FIF_DEBUG_MODULEP).Returns(
                new FrameInfo<SbModule>
                {
                    ValidFields = FrameInfoFlags.FIF_DEBUG_MODULEP,
                    Module = mockModule
                });
            var debugModule = Substitute.For<IDebugModule3>();
            _mockModuleCache.GetOrCreate(mockModule, _mockProgram).Returns(debugModule);

            var info = new FRAMEINFO[1];
            var fields = enum_FRAMEINFO_FLAGS.FIF_DEBUG_MODULEP;

            Assert.AreEqual(VSConstants.S_OK, _stackFrame.GetInfo(fields, 0, info));
            Assert.AreEqual(enum_FRAMEINFO_FLAGS.FIF_DEBUG_MODULEP,
                            info[0].m_dwValidFields & enum_FRAMEINFO_FLAGS.FIF_DEBUG_MODULEP);
            Assert.AreEqual(debugModule, info[0].m_pModule);
        }

        [Test]
        public void EnsureFrameInfoFlagsMatchAd7()
        {
            CollectionAssert.AreEqual(Enum.GetNames(typeof(enum_FRAMEINFO_FLAGS)),
                                      Enum.GetNames(typeof(FrameInfoFlags)));

            Assert.AreEqual(Enum.GetUnderlyingType(typeof(enum_FRAMEINFO_FLAGS)),
                            Enum.GetUnderlyingType(typeof(FrameInfoFlags)));

            Type underlyingType = Enum.GetUnderlyingType(typeof(FrameInfoFlags));
            Array ad7Values = Enum.GetValues(typeof(enum_FRAMEINFO_FLAGS));
            Array customValues = Enum.GetValues(typeof(FrameInfoFlags));
            for (int i = 0; i < ad7Values.Length; ++i)
            {
                Assert.AreEqual(Convert.ChangeType(ad7Values.GetValue(i), underlyingType),
                                Convert.ChangeType(customValues.GetValue(i), underlyingType));
            }
        }

        [Test]
        public void GetPropertyProvider()
        {
            Guid guidFilter = FrameVariablesProvider.PropertyFilterGuids.AllLocals;

            int status = _stackFrame.GetPropertyProvider(
                (uint) enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME, 0, ref guidFilter, 0,
                out IAsyncDebugPropertyInfoProvider provider);

            Assert.AreEqual(VSConstants.S_OK, status);
            Assert.IsNotNull(provider);
            Assert.IsInstanceOf<AsyncDebugRootPropertyInfoProvider>(provider);
        }
    }
}
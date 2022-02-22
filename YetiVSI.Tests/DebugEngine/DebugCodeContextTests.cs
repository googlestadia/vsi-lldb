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
using DebuggerApi;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using NSubstitute;
using NUnit.Framework;
using YetiCommon.CastleAspects;
using YetiVSI.DebugEngine;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class DebugCodeContextTests
    {
        const ulong _testPc = 0x123456789abcdef0;
        const string _testPcStr = "0x123456789abcdef0";
        const string _testName = "frame name";

        DebugCodeContext.Factory _codeContextfactory = new DebugCodeContext.Factory();

        RemoteTarget _target;
        IDebugDocumentContext2 _mockDocumentContext;
        IDebugCodeContext2 _codeContext;

        [SetUp]
        public void SetUp()
        {
            _target = Substitute.For<RemoteTarget>();
            _mockDocumentContext = Substitute.For<IDebugDocumentContext2>();
            _codeContext = _codeContextfactory.Create(
                _target, _testPc, _testName, _mockDocumentContext, Guid.Empty);
        }

        [Test]
        public void GetName()
        {
            Assert.AreEqual(VSConstants.S_OK, _codeContext.GetName(out string name));
            Assert.AreEqual(_testName, name);
        }

        [Test]
        public void GetNameNull()
        {
            _codeContext = _codeContextfactory.Create(
                _target, _testPc, null, _mockDocumentContext);
            Assert.AreEqual(VSConstants.S_OK, _codeContext.GetName(out string name));
            Assert.AreEqual(_testPcStr, name);
        }

        [Test]
        public void GetNameResolvedFromAddress()
        {
            var address = Substitute.For<SbAddress>();
            address.GetFunction().GetName().Returns("dummy_func()");
            _target.ResolveLoadAddress(_testPc).Returns(address);

            _codeContext = _codeContextfactory.Create(
                _target, _testPc, functionName: null, documentContext: null);

            Assert.AreEqual(VSConstants.S_OK, _codeContext.GetName(out string name));
            Assert.AreEqual("dummy_func()", name);
        }

        [Test]
        public void GetInfoAddress()
        {
            var contextInfo = new CONTEXT_INFO[1];
            Assert.AreEqual(VSConstants.S_OK,
                _codeContext.GetInfo(enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS, contextInfo));
            Assert.AreEqual(enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS, contextInfo[0].dwFields);
            Assert.AreEqual(_testPcStr, contextInfo[0].bstrAddress);
        }

        [Test]
        public void GetInfoName()
        {
            var contextInfo = new CONTEXT_INFO[1];
            Assert.AreEqual(VSConstants.S_OK,
                _codeContext.GetInfo(enum_CONTEXT_INFO_FIELDS.CIF_FUNCTION, contextInfo));
            Assert.AreEqual(enum_CONTEXT_INFO_FIELDS.CIF_FUNCTION, contextInfo[0].dwFields);
            Assert.AreEqual(_testName, contextInfo[0].bstrFunction);
        }

        [Test]
        public void GetInfo()
        {
            var contextInfo = new CONTEXT_INFO[1];
            Assert.AreEqual(VSConstants.S_OK,
                _codeContext.GetInfo(
                    enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS | enum_CONTEXT_INFO_FIELDS.CIF_FUNCTION,
                    contextInfo));
            Assert.AreEqual(
                enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS | enum_CONTEXT_INFO_FIELDS.CIF_FUNCTION,
                contextInfo[0].dwFields);
            Assert.AreEqual(_testPcStr, contextInfo[0].bstrAddress);
            Assert.AreEqual(_testName, contextInfo[0].bstrFunction);
        }

        [Test]
        public void GetInfoNone()
        {
            var contextInfo = new CONTEXT_INFO[1];
            Assert.AreEqual(VSConstants.S_OK,
                _codeContext.GetInfo(0, contextInfo));
            Assert.AreEqual(0, (uint)contextInfo[0].dwFields);
        }

        [Test]
        public void CompareNone()
        {
            Assert.AreEqual(VSConstants.S_FALSE, _codeContext.Compare(
                enum_CONTEXT_COMPARE.CONTEXT_EQUAL, null, 0, out _));
        }

        [Test]
        public void CompareEqual()
        {
            uint matchIndex;
            Assert.AreEqual(VSConstants.S_OK, _codeContext.Compare(
                enum_CONTEXT_COMPARE.CONTEXT_EQUAL,
                new IDebugMemoryContext2[1] { _codeContext}, 1, out matchIndex));
            Assert.AreEqual(0, matchIndex);
        }

        [Test]
        public void CompareGreaterThan()
        {
            Assert.AreEqual(VSConstants.S_OK, _codeContext.Compare(
                enum_CONTEXT_COMPARE.CONTEXT_GREATER_THAN,
                new IDebugMemoryContext2[1]
                {
                    _codeContextfactory.Create(
                        _target, _testPc - 1, _testName, _mockDocumentContext, Guid.Empty)
                },
                1, out uint matchIndex));
            Assert.AreEqual(0, matchIndex);
        }

        [Test]
        public void CompareGreaterThanOrEqual()
        {
            Assert.AreEqual(VSConstants.S_OK, _codeContext.Compare(
                enum_CONTEXT_COMPARE.CONTEXT_GREATER_THAN_OR_EQUAL,
                new IDebugMemoryContext2[1] { _codeContext }, 1, out uint matchIndex));
            Assert.AreEqual(0, matchIndex);
        }

        [Test]
        public void CompareLessThan()
        {
            Assert.AreEqual(VSConstants.S_OK, _codeContext.Compare(
                enum_CONTEXT_COMPARE.CONTEXT_LESS_THAN,
                new IDebugMemoryContext2[1]
                {
                    _codeContextfactory.Create(
                        _target, _testPc + 1, _testName, _mockDocumentContext, Guid.Empty)
                },
                1, out uint matchIndex));
            Assert.AreEqual(0, matchIndex);
        }

        [Test]
        public void CompareLessThanOrEqual()
        {
            Assert.AreEqual(VSConstants.S_OK, _codeContext.Compare(
                enum_CONTEXT_COMPARE.CONTEXT_LESS_THAN_OR_EQUAL,
                new IDebugMemoryContext2[1] { _codeContext }, 1, out uint matchIndex));
            Assert.AreEqual(0, matchIndex);
        }

        [Test]
        public void CompareSameFunction()
        {
            Assert.AreEqual(VSConstants.S_OK, _codeContext.Compare(
                enum_CONTEXT_COMPARE.CONTEXT_SAME_FUNCTION,
                new IDebugMemoryContext2[1] { _codeContext }, 1, out uint matchIndex));
            Assert.AreEqual(0, matchIndex);
        }

        [Test]
        public void CompareMultiple()
        {
            IDebugMemoryContext2[] others = new IDebugMemoryContext2[]
            {
                _codeContextfactory.Create(
                    _target, _testPc - 1, _testName, _mockDocumentContext, Guid.Empty),
                _codeContextfactory.Create(
                    _target, _testPc, _testName, _mockDocumentContext, Guid.Empty),
                _codeContextfactory.Create(
                    _target, _testPc + 1, _testName, _mockDocumentContext, Guid.Empty)
            };
            uint matchIndex;
            Assert.AreEqual(VSConstants.S_OK, _codeContext.Compare(
                enum_CONTEXT_COMPARE.CONTEXT_EQUAL, others, 3, out matchIndex));
            Assert.AreEqual(1, matchIndex);
        }

        [Test]
        public void CompareEqualWithProxy()
        {
            var proxyGenerator = new Castle.DynamicProxy.ProxyGenerator();
            var decoratorUtil = new DecoratorUtil();

            // Create the factory.
            var factory = new DebugCodeContext.Factory();

            // Decorate the factory with a dummy aspect.
            var aspect = new YetiCommon.Tests.CastleAspects.TestSupport.CallCountAspect();
            var factoryDecorator = decoratorUtil.CreateFactoryDecorator(proxyGenerator, aspect);
            var factoryWithProxy = factoryDecorator.Decorate(factory);

            var memoryContextWithProxy = factoryWithProxy.Create(
                _target, _testPc, _testName, _mockDocumentContext, Guid.Empty);

            // Check all the combinations of comparing proxied and non-proxied memory context.
            uint matchIndex;
            Assert.AreEqual(VSConstants.S_OK, memoryContextWithProxy.Compare(
                enum_CONTEXT_COMPARE.CONTEXT_EQUAL,
                new IDebugMemoryContext2[1] { memoryContextWithProxy }, 1, out matchIndex));
            Assert.AreEqual(0, matchIndex);

            Assert.AreEqual(VSConstants.S_OK, _codeContext.Compare(
                enum_CONTEXT_COMPARE.CONTEXT_EQUAL,
                new IDebugMemoryContext2[1] { memoryContextWithProxy }, 1, out matchIndex));
            Assert.AreEqual(0, matchIndex);

            Assert.AreEqual(VSConstants.S_OK, memoryContextWithProxy.Compare(
                enum_CONTEXT_COMPARE.CONTEXT_EQUAL,
                new IDebugMemoryContext2[1] { _codeContext}, 1, out matchIndex));
            Assert.AreEqual(0, matchIndex);
        }

        [Test]
        public void GetAddress()
        {
            Assert.AreEqual(_testPc, ((IGgpDebugCodeContext)_codeContext).Address);
        }

        [Test]
        public void GetDocumentContext()
        {
            Assert.AreEqual(VSConstants.S_OK, _codeContext.GetDocumentContext(
                                                  out IDebugDocumentContext2 documentContext));
            Assert.AreEqual(_mockDocumentContext, documentContext);
        }

        [Test]
        public void GetDocumentContext_Null()
        {
            _codeContext = _codeContextfactory.Create(
                _target, _testPc, _testName, null, Guid.Empty);

            Assert.AreEqual(VSConstants.S_FALSE, _codeContext.GetDocumentContext(
                                                     out IDebugDocumentContext2 documentContext));
            Assert.AreEqual(null, documentContext);
        }

        [Test]
        public void GetLanguageInfo()
        {
            string language = null;
            var guid = new Guid();
            Assert.AreEqual(VSConstants.S_OK, _codeContext.GetLanguageInfo(ref language, ref guid));
            Assert.That(guid, Is.EqualTo(Guid.Empty));
        }

        [TestCase(AD7Constants.CLanguage, "C")]
        [TestCase(AD7Constants.CppLanguage, "C++")]
        [TestCase(AD7Constants.CSharpLanguage, "C#")]
        [TestCase("63A08714-FC37-11D2-904C-00C04FA302A2", "")]
        [TestCase("00000000-0000-0000-0000-000000000000", "")]
        public void GetLanguageInfoWhenProvided(string guidAsString, string correspondingLanguage)
        {
            var guid = new Guid(guidAsString);
            IDebugCodeContext2 context = _codeContextfactory.Create(
                _target, _testPc, _testName, _mockDocumentContext, guid);

            var outputGuid = Guid.Empty;
            var outputLanguage = string.Empty;
            int result = context.GetLanguageInfo(ref outputLanguage, ref outputGuid);
            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
            Assert.That(outputGuid, Is.EqualTo(outputGuid));
            Assert.That(outputLanguage, Is.EqualTo(correspondingLanguage));
        }
    }
}

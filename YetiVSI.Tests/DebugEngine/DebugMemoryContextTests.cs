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

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using NSubstitute;
using NUnit.Framework;
using YetiCommon.CastleAspects;
using YetiVSI.DebugEngine;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class DebugMemoryContextTests
    {
        const ulong TEST_PC = 0x123456789abcdef0;
        const string TEST_PC_STR = "0x123456789abcdef0";
        const string TEST_NAME = "frame name";

        DebugMemoryContext.Factory mockMemoryContextFactory;

        DebugMemoryContext.Factory memoryContextFactory;
        IDebugMemoryContext2 memoryContext;

        [SetUp]
        public void SetUp()
        {
            mockMemoryContextFactory = Substitute.For<DebugMemoryContext.Factory>();
            memoryContextFactory = new DebugMemoryContext.Factory(mockMemoryContextFactory);
            memoryContext = memoryContextFactory.Create(TEST_PC, TEST_NAME);
        }

        [Test]
        public void GetName()
        {
            string name;
            Assert.AreEqual(VSConstants.S_OK, memoryContext.GetName(out name));
            Assert.AreEqual(TEST_NAME, name);
        }

        [Test]
        public void GetNameNull()
        {
            string name;
            memoryContext = memoryContextFactory.Create(TEST_PC, null);
            Assert.AreEqual(VSConstants.S_OK, memoryContext.GetName(out name));
            Assert.AreEqual($"{TEST_PC_STR}",name);
        }

        [Test]
        public void GetInfoAddress()
        {
            var contextInfo = new CONTEXT_INFO[1];
            Assert.AreEqual(VSConstants.S_OK,
                memoryContext.GetInfo(enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS, contextInfo));
            Assert.AreEqual(enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS, contextInfo[0].dwFields);
            Assert.AreEqual(TEST_PC_STR, contextInfo[0].bstrAddress);
        }

        [Test]
        public void GetInfoName()
        {
            var contextInfo = new CONTEXT_INFO[1];
            Assert.AreEqual(VSConstants.S_OK,
                memoryContext.GetInfo(enum_CONTEXT_INFO_FIELDS.CIF_FUNCTION, contextInfo));
            Assert.AreEqual(enum_CONTEXT_INFO_FIELDS.CIF_FUNCTION, contextInfo[0].dwFields);
            Assert.AreEqual(TEST_NAME, contextInfo[0].bstrFunction);
        }

        [Test]
        public void GetInfo()
        {
            var contextInfo = new CONTEXT_INFO[1];
            Assert.AreEqual(VSConstants.S_OK,
                memoryContext.GetInfo(
                    enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS | enum_CONTEXT_INFO_FIELDS.CIF_FUNCTION,
                    contextInfo));
            Assert.AreEqual(
                enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS | enum_CONTEXT_INFO_FIELDS.CIF_FUNCTION,
                contextInfo[0].dwFields);
            Assert.AreEqual(TEST_PC_STR, contextInfo[0].bstrAddress);
            Assert.AreEqual(TEST_NAME, contextInfo[0].bstrFunction);
        }

        [Test]
        public void GetInfoNone()
        {
            var contextInfo = new CONTEXT_INFO[1];
            Assert.AreEqual(VSConstants.S_OK,
                memoryContext.GetInfo(0, contextInfo));
            Assert.AreEqual(0, (uint)contextInfo[0].dwFields);
        }

        [Test]
        public void Add()
        {
            const ulong COUNT = 4;
            IDebugMemoryContext2 result;
            Assert.AreEqual(VSConstants.S_OK, memoryContext.Add(COUNT, out result));
            mockMemoryContextFactory.Received(1).Create(TEST_PC + COUNT, TEST_NAME);
        }

        [Test]
        public void Substract()
        {
            const ulong COUNT = 4;
            IDebugMemoryContext2 result;
            Assert.AreEqual(VSConstants.S_OK, memoryContext.Subtract(COUNT, out result));
            mockMemoryContextFactory.Received(1).Create(TEST_PC - COUNT, TEST_NAME);
        }

        [Test]
        public void CompareNone()
        {
            uint matchIndex;
            Assert.AreEqual(VSConstants.S_FALSE, memoryContext.Compare(
                enum_CONTEXT_COMPARE.CONTEXT_EQUAL, null, 0, out matchIndex));
        }

        [Test]
        public void CompareEqual()
        {
            uint matchIndex;
            Assert.AreEqual(VSConstants.S_OK, memoryContext.Compare(
                enum_CONTEXT_COMPARE.CONTEXT_EQUAL,
                new IDebugMemoryContext2[1] { memoryContext }, 1, out matchIndex));
            Assert.AreEqual(0, matchIndex);
        }

        [Test]
        public void CompareGreaterThan()
        {
            uint matchIndex;
            Assert.AreEqual(VSConstants.S_OK, memoryContext.Compare(
                enum_CONTEXT_COMPARE.CONTEXT_GREATER_THAN,
                new IDebugMemoryContext2[1] { memoryContextFactory.Create(TEST_PC - 1, TEST_NAME) },
                1, out matchIndex));
            Assert.AreEqual(0, matchIndex);
        }

        [Test]
        public void CompareGreaterThanOrEqual()
        {
            uint matchIndex;
            Assert.AreEqual(VSConstants.S_OK, memoryContext.Compare(
                enum_CONTEXT_COMPARE.CONTEXT_GREATER_THAN_OR_EQUAL,
                new IDebugMemoryContext2[1] { memoryContext }, 1, out matchIndex));
            Assert.AreEqual(0, matchIndex);
        }

        [Test]
        public void CompareLessThan()
        {
            uint matchIndex;
            Assert.AreEqual(VSConstants.S_OK, memoryContext.Compare(
                enum_CONTEXT_COMPARE.CONTEXT_LESS_THAN,
                new IDebugMemoryContext2[1] { memoryContextFactory.Create(TEST_PC + 1, TEST_NAME) },
                1, out matchIndex));
            Assert.AreEqual(0, matchIndex);
        }

        [Test]
        public void CompareLessThanOrEqual()
        {
            uint matchIndex;
            Assert.AreEqual(VSConstants.S_OK, memoryContext.Compare(
                enum_CONTEXT_COMPARE.CONTEXT_LESS_THAN_OR_EQUAL,
                new IDebugMemoryContext2[1] { memoryContext }, 1, out matchIndex));
            Assert.AreEqual(0, matchIndex);
        }

        [Test]
        public void CompareSameFunction()
        {
            uint matchIndex;
            Assert.AreEqual(VSConstants.S_OK, memoryContext.Compare(
                enum_CONTEXT_COMPARE.CONTEXT_SAME_FUNCTION,
                new IDebugMemoryContext2[1] { memoryContext }, 1, out matchIndex));
            Assert.AreEqual(0, matchIndex);
        }

        [Test]
        public void CompareMultiple()
        {
            IDebugMemoryContext2[] others = new IDebugMemoryContext2[]
            {
                memoryContextFactory.Create(TEST_PC - 1, TEST_NAME),
                memoryContextFactory.Create(TEST_PC, TEST_NAME),
                memoryContextFactory.Create(TEST_PC + 1, TEST_NAME)
            };
            uint matchIndex;
            Assert.AreEqual(VSConstants.S_OK, memoryContext.Compare(
                enum_CONTEXT_COMPARE.CONTEXT_EQUAL, others, 3, out matchIndex));
            Assert.AreEqual(1, matchIndex);
        }

        [Test]
        public void CompareEqualWithProxy()
        {
            var proxyGenerator = new Castle.DynamicProxy.ProxyGenerator();
            var decoratorUtil = new DecoratorUtil();

            // Create the factory.
            var factory = new DebugMemoryContext.Factory();

            // Decorate the factory with a dummy aspect.
            var aspect = new YetiCommon.Tests.CastleAspects.TestSupport.CallCountAspect();
            var factoryDecorator = decoratorUtil.CreateFactoryDecorator(proxyGenerator, aspect);
            var factoryWithProxy = factoryDecorator.Decorate(factory);

            var memoryContextWithProxy = factoryWithProxy.Create(TEST_PC, TEST_NAME);

            // Check all the combinations of comparing proxied and non-proxied memory context.
            uint matchIndex;
            Assert.AreEqual(VSConstants.S_OK, memoryContextWithProxy.Compare(
                enum_CONTEXT_COMPARE.CONTEXT_EQUAL,
                new IDebugMemoryContext2[1] { memoryContextWithProxy }, 1, out matchIndex));
            Assert.AreEqual(0, matchIndex);

            Assert.AreEqual(VSConstants.S_OK, memoryContext.Compare(
                enum_CONTEXT_COMPARE.CONTEXT_EQUAL,
                new IDebugMemoryContext2[1] { memoryContextWithProxy }, 1, out matchIndex));
            Assert.AreEqual(0, matchIndex);

            Assert.AreEqual(VSConstants.S_OK, memoryContextWithProxy.Compare(
                enum_CONTEXT_COMPARE.CONTEXT_EQUAL,
                new IDebugMemoryContext2[1] { memoryContext }, 1, out matchIndex));
            Assert.AreEqual(0, matchIndex);
        }
    }
}
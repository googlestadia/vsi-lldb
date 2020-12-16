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

﻿using DebuggerApi;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YetiVSI.DebugEngine;
using YetiVSI.DebugEngine.Variables;
using YetiVSI.Test.TestSupport;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class RegisterSetsBuilderTests
    {
        RemoteFrame mockFrame;

        IRegisterSetsBuilder registerSetsBuilder;

        RemoteValue generalPurposeRegisters, floatingPointRegisters;
        RemoteValue xmm0, xmm8, other;

        [SetUp]
        public void SetUp()
        {
            mockFrame = Substitute.For<RemoteFrame>();

            var childAdapterFactory = new RemoteValueChildAdapter.Factory();
            var varInfoFactory = new LLDBVariableInformationFactory(childAdapterFactory);
            var varInfoBuilder = new VarInfoBuilder(varInfoFactory);
            varInfoFactory.SetVarInfoBuilder(varInfoBuilder);
            var registerSetsBuilderFactory = new RegisterSetsBuilder.Factory(varInfoFactory);
            registerSetsBuilder = registerSetsBuilderFactory.Create(mockFrame);

            generalPurposeRegisters = Substitute.For<RemoteValue>();
            generalPurposeRegisters.GetName().Returns("General Purpose Registers");
            generalPurposeRegisters.GetNumChildren().Returns(0u);

            floatingPointRegisters = Substitute.For<RemoteValue>();
            floatingPointRegisters.GetName().Returns("Floating Point Registers");
            floatingPointRegisters.GetNumChildren().Returns(3u);
            xmm0 = Substitute.For<RemoteValue>();
            xmm0.GetName().Returns("xmm0");
            xmm8 = Substitute.For<RemoteValue>();
            xmm8.GetName().Returns("xmm8");
            other = Substitute.For<RemoteValue>();
            other.GetName().Returns("other");
            floatingPointRegisters.GetChildren(0, 3).Returns(
                new List<RemoteValue>() { xmm0, xmm8, other });
        }

        [Test]
        public void BuildSetsWhenValuesListIsEmpty()
        {
            mockFrame.GetRegisters().Returns(new List<RemoteValue>());

            var sets = registerSetsBuilder.BuildSets().ToList();
            Assert.IsEmpty(sets);
        }

        [Test]
        public void BuildSetsWhenFPRCanNotBeFound()
        {
            mockFrame.GetRegisters().Returns(new List<RemoteValue> { generalPurposeRegisters });

            var sets = registerSetsBuilder.BuildSets().ToList();
            Assert.AreEqual(1, sets.Count);
            Assert.AreEqual("CPU", sets[0].DisplayName);
        }

        [Test]
        public async Task BuildSetsWithRealAndSyntheticRegisterSetsAsync()
        {
            mockFrame.GetRegisters().Returns(
                new List<RemoteValue> { generalPurposeRegisters, floatingPointRegisters });

            var sets = registerSetsBuilder.BuildSets().ToList();
            Assert.AreEqual(4, sets.Count);
            IVariableInformation cpu = sets[0];
            Assert.AreEqual("CPU", cpu.DisplayName);
            IEnumerable<IVariableInformation> gprChildren = await cpu.GetAllChildrenAsync();
            Assert.AreEqual(0, gprChildren.Count());
            Assert.AreEqual(CustomVisualizer.None, cpu.CustomVisualizer);

            IVariableInformation fpr = sets[1];
            Assert.AreEqual("Floating Point Registers", fpr.DisplayName);
            IEnumerable<IVariableInformation> fprChildren = await fpr.GetAllChildrenAsync();
            CollectionAssert.AreEqual(
                new[] { "xmm0", "xmm8", "other" }, fprChildren.Select(r => r.DisplayName));
            Assert.AreEqual(CustomVisualizer.None, fpr.CustomVisualizer);

            IVariableInformation sse = sets[2];
            Assert.AreEqual("SSE", sse.DisplayName);
            IEnumerable<IVariableInformation> sseChildren = await sse.GetAllChildrenAsync();
            CollectionAssert.AreEqual(
                new[] { "xmm0", "xmm8", "other" }, sseChildren.Select(r => r.DisplayName));
            Assert.AreEqual(CustomVisualizer.SSE, sse.CustomVisualizer);

            IVariableInformation sse2 = sets[3];
            Assert.AreEqual("SSE2", sse2.DisplayName);
            IEnumerable<IVariableInformation> sse2Children = await sse2.GetAllChildrenAsync();
            CollectionAssert.AreEqual(
                new[] { "xmm0", "xmm8", "other" }, sse2Children.Select(r => r.DisplayName));
            Assert.AreEqual(CustomVisualizer.SSE2, sse2.CustomVisualizer);
        }
    }
}

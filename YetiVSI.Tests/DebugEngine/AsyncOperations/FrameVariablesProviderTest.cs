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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YetiVSI.DebugEngine;
using YetiVSI.DebugEngine.AsyncOperations;
using YetiVSI.DebugEngine.Variables;
using YetiVSI.Test.TestSupport;

namespace YetiVSI.Test.DebugEngine.AsyncOperations
{
    [TestFixture]
    public class FrameVariablesProviderTest
    {
        FrameVariablesProvider _frameVariablesProvider;
        IRegisterSetsBuilder _registerSetsBuilder;
        RemoteFrame _mockRemoteFrame;
        IVariableInformationFactory _varInfoFactory;

        [SetUp]
        public void SetUp()
        {
            _mockRemoteFrame = Substitute.For<RemoteFrame>();
            var childAdapterFactory = new RemoteValueChildAdapter.Factory();
            _varInfoFactory = new LLDBVariableInformationFactory(childAdapterFactory);
            _registerSetsBuilder = new RegisterSetsBuilder.Factory(_varInfoFactory)
                .Create(_mockRemoteFrame);

            _frameVariablesProvider = new FrameVariablesProvider(_registerSetsBuilder,
                _mockRemoteFrame, _varInfoFactory);

            SetupAllVariables();
        }

        [Test]
        public void GetRegisters()
        {
            var result = _frameVariablesProvider.Get(
                FrameVariablesProvider.PropertyFilterGuids.Registers)?.ToList();

            Assert.IsNotNull(result);
            Assert.That(result.Count, Is.EqualTo(4));
            Assert.That(result[0].DisplayName, Is.EqualTo("CPU"));
            Assert.That(result[1].DisplayName, Is.EqualTo("Floating Point Registers"));
            Assert.That(result[2].DisplayName, Is.EqualTo("SSE"));
            Assert.That(result[3].DisplayName, Is.EqualTo("SSE2"));
        }

        [TestCase(true, TestName = "GetAllLocals")]
        [TestCase(false, TestName = "GetLocals")]
        public void GetLocals(bool all)
        {
            var filter = all ? FrameVariablesProvider.PropertyFilterGuids.AllLocals :
                FrameVariablesProvider.PropertyFilterGuids.Locals;

            var result = _frameVariablesProvider.Get(filter)?.ToList();

            Assert.IsNotNull(result);
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result[0].DisplayName, Is.EqualTo("value1"));
            Assert.That(result[1].DisplayName, Is.EqualTo("value2"));
        }

        [Test]
        public void GetArguments()
        {
            var result = _frameVariablesProvider.Get(
                FrameVariablesProvider.PropertyFilterGuids.Arguments)?.ToList();

            Assert.IsNotNull(result);
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result[0].DisplayName, Is.EqualTo("intVal"));
            Assert.That(result[1].DisplayName, Is.EqualTo("boolVal"));
        }

        [TestCase(true, TestName = "GetAllLocalsPlusArguments")]
        [TestCase(false, TestName = "GetLocalsPlusArguments")]
        public void GetLocalsPlusArguments(bool all)
        {
            SetupAllVariables();
            var filter = all ? FrameVariablesProvider.PropertyFilterGuids.AllLocalsPlusArguments :
                FrameVariablesProvider.PropertyFilterGuids.LocalsPlusArguments;

            var result = _frameVariablesProvider.Get(filter)?.ToList();

            Assert.IsNotNull(result);
            Assert.That(result.Count, Is.EqualTo(4));
            Assert.That(result[0].DisplayName, Is.EqualTo("intVal"));
            Assert.That(result[1].DisplayName, Is.EqualTo("boolVal"));
            Assert.That(result[2].DisplayName, Is.EqualTo("value1"));
            Assert.That(result[3].DisplayName, Is.EqualTo("value2"));
        }

        public void GetByUnsupportedFilter()
        {
            var result = _frameVariablesProvider.Get(
                new Guid("12345678-1234-1234-1234-123456789123"))?.ToList();

            Assert.IsNull(result);
        }

        void SetupAllVariables()
        {
            SetupRegisters();
            SetupLocalAndArguments();
        }

        void SetupRegisters()
        {
            var registerSet1 = RemoteValueFakeUtil.CreateSimpleIntArray(
                "General Purpose Registers", 0x01, 0x02);
            var registerSet2 = RemoteValueFakeUtil.CreateSimpleArray(
                "Floating Point Registers", "Register Set",
                (_, value) => new RemoteValueFake($"xmm{value}", "0x00"),
                0, 1);

            _mockRemoteFrame.GetRegisters().Returns(
                new List<RemoteValue> { registerSet1, registerSet2 });
        }

        void SetupLocalAndArguments()
        {
            var local1 = RemoteValueFakeUtil.CreateSimpleInt("value1", 5);
            var local2 = RemoteValueFakeUtil.CreateSimpleInt("value2", 7);
            var arg1 = RemoteValueFakeUtil.CreateSimpleInt("intVal", 5);
            var arg2 = RemoteValueFakeUtil.CreateSimpleBool("boolVal", true);

            _mockRemoteFrame.GetVariables(false, true, false, true).Returns(
                new List<RemoteValue> { local1, local2 });
            _mockRemoteFrame.GetVariables(true, false, false, true).Returns(
                new List<RemoteValue> { arg1, arg2 });
            _mockRemoteFrame.GetVariables(true, true, false, true).Returns(
                new List<RemoteValue> { arg1, arg2, local1, local2 });
        }
    }
}

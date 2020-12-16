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
using TestsCommon.TestSupport;
using YetiVSI.DebugEngine.Variables;
using YetiVSI.Test.TestSupport;

namespace YetiVSI.Test.DebugEngine.Variables
{
    [TestFixture]
    class ValueStringBuilderTests
    {
        MediumTestDebugEngineFactoryCompRoot _compRoot;
        RemoteFrameStub _remoteFrame;
        RemoteValueFake _remoteValue;
        const ulong _pointerAddress = 1234;
        RemoteValueFake _pointerValue;
        int[] _childValues;

        [SetUp]
        public void SetUp()
        {
            _remoteFrame = new RemoteFrameStub();
            _compRoot = new MediumTestDebugEngineFactoryCompRoot();

            _childValues = new int[] { 20, 21, 22, 23, 24 };
            _remoteValue = RemoteValueFakeUtil.CreateSimpleIntArray("myArray", _childValues);

            ulong elementSize = 4;
            var pointeeType = new SbTypeStub("int", TypeFlags.IS_INTEGER);
            pointeeType.SetByteSize(elementSize);
            var pointerType = new SbTypeStub("int*", TypeFlags.IS_POINTER, pointeeType);
            _pointerValue =
                RemoteValueFakeUtil.CreatePointer("int*", "myPtr", $"{_pointerAddress}");
            _pointerValue.SetTypeInfo(pointerType);

            for (uint i = 0; i < 5; i++)
            {
                _pointerValue.SetCreateValueFromAddress(
                    _pointerAddress + i * elementSize,
                    RemoteValueFakeUtil.CreateSimpleInt($"[{i}]", (int)(i) + 20));
            }
        }

        [Test]
        public async Task Build_ArraySpecifiedNumberOfChildrenAsync()
        {
            IVariableInformation varInfo = CreateVarInfo(_remoteValue, "3");
            string result = await ValueStringBuilder.BuildAsync(varInfo);
            Assert.That(result, Is.EqualTo("{20, 21, 22}"));
        }

        [Test]
        public async Task Build_ArrayCapsAtArraySizeAsync()
        {
            IVariableInformation varInfo = CreateVarInfo(_remoteValue, "10");
            string result = await ValueStringBuilder.BuildAsync(varInfo);
            Assert.That(result, Is.EqualTo("{20, 21, 22, 23, 24}"));
        }

        [Test]
        public async Task Build_ArrayLimitsOutputSizeAsync()
        {
            const int size = 100;
            _childValues = Enumerable.Range(0, size - 1).ToArray();
            _remoteValue = RemoteValueFakeUtil.CreateSimpleIntArray("myArray", _childValues);

            IVariableInformation varInfo = CreateVarInfo(_remoteValue, size.ToString());
            string result = await ValueStringBuilder.BuildAsync(varInfo);
            Assert.That(
                result,
                Is.EqualTo(
                    "{0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20," +
                    " 21, ...}"));
        }

        [Test]
        public async Task Build_ClassValueAsync()
        {
            RemoteValueFake classValue = RemoteValueFakeUtil.CreateClass("C", "c", "");
            classValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("f", 42));
            classValue.AddChild(RemoteValueFakeUtil.CreateSimpleString("g", "\"s\""));

            IVariableInformation varInfo = CreateVarInfo(classValue, "3");
            string result = await ValueStringBuilder.BuildAsync(varInfo);
            Assert.That(result, Is.EqualTo("{f=42 g=\"s\"}"));
        }

        [Test]
        public async Task Build_PointerClassValueAsync()
        {
            string address = $"0x{_pointerAddress:x16}";
            RemoteValueFake pointer =
                RemoteValueFakeUtil.CreatePointer("C*", "pc", _pointerAddress.ToString());
            pointer.AddChild(RemoteValueFakeUtil.CreateSimpleInt("f", 42));
            pointer.AddChild(RemoteValueFakeUtil.CreateSimpleString("g", "\"s\""));

            IVariableInformation varInfo = CreateVarInfo(pointer, "");
            string result = await ValueStringBuilder.BuildAsync(varInfo);
            Assert.That(result, Is.EqualTo($"0x{_pointerAddress:x16} {{f=42 g=\"s\"}}"));
        }

        [Test]
        public async Task Build_PointerArraySpecifiedNumberOfChildrenAsync()
        {
            IVariableInformation varInfo = CreateVarInfo(_pointerValue, "7");
            string result = await ValueStringBuilder.BuildAsync(varInfo);
            Assert.That(
                result,
                Is.EqualTo(
                    $"0x{_pointerAddress:x16} {{20, 21, 22, 23, 24, <invalid>, <invalid>}}"));
        }

        IVariableInformation CreateVarInfo(RemoteValue remoteValue, string formatSpecifier) =>
            _compRoot.GetVariableInformationFactory().Create(_remoteFrame, remoteValue,
                                                             remoteValue.GetName(),
                                                             new FormatSpecifier(formatSpecifier));
    }
}

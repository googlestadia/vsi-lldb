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
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using TestsCommon.TestSupport;
using YetiVSI.DebugEngine.Variables;
using YetiVSI.Test.TestSupport;

namespace YetiVSI.Test.DebugEngine.Variables
{
    [TestFixture]
    class RemoteValueArraySizeFormatTests
    {
        LogSpy logSpy;
        RemoteValueFake remoteValue;
        RemoteValueFake pointerValue;
        int[] childValues;

        [SetUp]
        public void SetUp()
        {
            logSpy = new LogSpy();
            logSpy.Attach();

            childValues = new int[] { 20, 21, 22, 23, 24 };
            remoteValue = RemoteValueFakeUtil.CreateSimpleIntArray("myArray", childValues);

            ulong elementSize = 4;
            var pointeeType = new SbTypeStub("int", TypeFlags.IS_INTEGER);
            pointeeType.SetByteSize(elementSize);
            var pointerType = new SbTypeStub("int*", TypeFlags.IS_POINTER, pointeeType);
            ulong address = 1234;
            pointerValue = RemoteValueFakeUtil.CreatePointer("int*", "myPtr", $"{address}");
            pointerValue.SetTypeInfo(pointerType);

            for (uint i = 0; i < 5; i++)
            {
                pointerValue.SetCreateValueFromAddress(
                    address + i * elementSize,
                    RemoteValueFakeUtil.CreateSimpleInt($"[{i}]", (int)(i) + 20));
            }
        }

        [TearDown]
        public void TearDown()
        {
            logSpy.Detach();
        }

        [Test]
        public void TestGetNumChildren_WhenSizeLessThanNumChildren()
        {
            const int castArraySize = 3;
            Assert.That(castArraySize, Is.LessThan(childValues.Length));

            RemoteValueFormat format = GetFormat(castArraySize);
            uint result = format.GetNumChildren(pointerValue);

            Assert.That(result, Is.EqualTo(castArraySize));
        }

        [Test]
        public void TestGetChildren_WhenSizeGreaterThanNumChildren()
        {
            const int castArraySize = 10;
            Assert.That(castArraySize, Is.GreaterThan(childValues.Length));

            RemoteValueFormat format = GetFormat(castArraySize);
            string[] result = format.GetChildren(pointerValue, 0, castArraySize)
                                  .Select(v => v.GetDefaultValue())
                                  .ToArray();

            string[] expected =
                childValues.Select(v => v.ToString()).Concat(Enumerable.Repeat("", 5)).ToArray();

            Assert.That(result.Length, Is.EqualTo(expected.Length));
            Assert.That(result, Is.EquivalentTo(expected));
        }

        [Test]
        public void TestGetChildren_WhenSizeLessThanNumChildren()
        {
            const int castArraySize = 3;
            RemoteValueFormat format = GetFormat(castArraySize);

            // Try to get more to make sure that |format| stops at |castArraySize|.
            int[] result = format.GetChildren(pointerValue, 0, castArraySize + 1)
                               .Select(v => int.Parse(v.GetDefaultValue()))
                               .ToArray();

            Assert.That(result.Length, Is.EqualTo(castArraySize));
            Assert.That(result, Is.EquivalentTo(childValues.Take(castArraySize)));
        }

        [Test]
        public void TestGetChildren_Pointer()
        {
            const int requestedArraySize = 3;
            RemoteValueFormat format = GetFormat(requestedArraySize);
            List<RemoteValue> result =
                format.GetChildren(pointerValue, 1, requestedArraySize).ToList();

            Assert.That(result.Count(), Is.EqualTo(2));
            Assert.That(result[0].GetDefaultValue, Is.EqualTo("21"));
            Assert.That(result[0].GetName(), Is.EqualTo("[1]"));
            Assert.That(result[1].GetDefaultValue, Is.EqualTo("22"));
            Assert.That(result[1].GetName(), Is.EqualTo("[2]"));
        }

        [Test]
        public void TestGetChildren_Array()
        {
            const int requestedArraySize = 3;
            RemoteValueFormat format = GetFormat(requestedArraySize);
            List<RemoteValue> result =
                format.GetChildren(remoteValue, 1, requestedArraySize).ToList();

            Assert.That(result.Count(), Is.EqualTo(2));
            Assert.That(result[0].GetDefaultValue, Is.EqualTo("21"));
            Assert.That(result[0].GetName(), Is.EqualTo("[1]"));
            Assert.That(result[1].GetDefaultValue, Is.EqualTo("22"));
            Assert.That(result[1].GetName(), Is.EqualTo("[2]"));
        }

        [Test]
        public void TestGetChildren_Pointer_Empty()
        {
            const int requestedArraySize = 0;
            RemoteValueFormat format = GetFormat(requestedArraySize);
            List<RemoteValue> result =
                format.GetChildren(pointerValue, 1, requestedArraySize).ToList();

            Assert.That(result.Count(), Is.EqualTo(0));
        }

        [Test]
        public void TestGetChildren_Pointer_NoPointee()
        {
            const int requestedArraySize = 3;
            RemoteValueFormat format = GetFormat(requestedArraySize);
            var pointerValueNoPointee = RemoteValueFakeUtil.CreatePointer("C*", "pc", "1234");
            List<RemoteValue> result =
                format.GetChildren(pointerValueNoPointee, 1, requestedArraySize).ToList();

            Assert.That(result.Count(), Is.EqualTo(0));
        }

        [Test]
        public void TestShouldInheritFormatSpecifier()
        {
            Assert.That(GetFormat(3).ShouldInheritFormatSpecifier(), Is.False);
        }

        RemoteValueFormat GetFormat(uint size) =>
            new RemoteValueFormat(RemoteValueDefaultFormat.DefaultFormatter,
                                  sizeSpecifier: new ScalarNumChildrenProvider(size));
    }
}

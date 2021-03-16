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

using DebuggerCommonApi;
using LldbApi;
using NSubstitute;
using NUnit.Framework;
using System;

namespace DebuggerGrpcServer.Tests
{
    [TestFixture]
    [Timeout(5000)]
    class RemoteThreadTests
    {
        SbThread mockThread;
        RemoteThread remoteThread;

        [SetUp]
        public void SetUp()
        {
            mockThread = Substitute.For<SbThread>();
            var optionsFactory = Substitute.For<ILldbExpressionOptionsFactory>();
            var valueFactory = new RemoteValueImpl.Factory(optionsFactory);
            remoteThread = new RemoteThreadImpl.Factory(
                new RemoteFrameImpl.Factory(valueFactory, optionsFactory)).Create(mockThread);
        }

        [Test]
        public void GetFrameAtIndexReturnsNullFrame()
        {
            mockThread.GetFrameAtIndex(0).Returns((SbFrame)null);
            Assert.Null(remoteThread.GetFrameAtIndex(0));
        }

        [Test]
        public void GetStopReasonConvertsAllEnumTypesCorrectly()
        {
            foreach (var reason in Enum.GetValues(typeof(LldbApi.StopReason)))
            {
                mockThread.GetStopReason().Returns(reason);
                var convertedReason = remoteThread.GetStopReason();
                Assert.AreEqual(reason, Enum.Parse(
                    typeof(LldbApi.StopReason), convertedReason.ToString()));
            }
        }

        [Test]
        public void GetFramesWithInfoSuccess()
        {
            var mockFrame0 = Substitute.For<SbFrame>();
            mockFrame0.GetFunctionName().Returns("function0");
            mockThread.GetFrameAtIndex(0).Returns(mockFrame0);

            var mockFrame1 = Substitute.For<SbFrame>();
            mockFrame1.GetFunctionName().Returns("function1");
            mockThread.GetFrameAtIndex(1).Returns(mockFrame1);

            var mockFrame2 = Substitute.For<SbFrame>();
            mockFrame2.GetFunctionName().Returns("function2(int (*)(int), int)");
            mockThread.GetFrameAtIndex(2).Returns(mockFrame2);

            mockThread.GetFrameAtIndex(3).Returns((SbFrame)null);

            var framesInfo = remoteThread
                .GetFramesWithInfo(FrameInfoFlags.FIF_FUNCNAME, startIndex: 0, maxCount: 10);

            Assert.AreEqual(3, framesInfo.Count);
            Assert.AreEqual("function0", framesInfo[0].Frame.GetFunctionName());
            Assert.NotNull(framesInfo[0].Info);
            Assert.AreEqual("function1", framesInfo[1].Frame.GetFunctionName());
            Assert.NotNull(framesInfo[1].Info);
            Assert.AreEqual("function2", framesInfo[2].Frame.GetFunctionName());
            Assert.NotNull(framesInfo[2].Info);
        }

        [Test]
        public void GetFramesWithInfoCapsListAtMaxStackDepth()
        {
            var mockFrame0 = Substitute.For<SbFrame>();
            mockFrame0.GetFunctionName().Returns("function0");
            mockThread.GetFrameAtIndex(0).Returns(mockFrame0);

            var mockFrame1 = Substitute.For<SbFrame>();
            mockFrame1.GetFunctionName().Returns("function1");
            mockThread.GetFrameAtIndex(1).Returns(mockFrame1);

            var mockFrame2 = Substitute.For<SbFrame>();
            mockFrame2.GetFunctionName().Returns("function2");
            mockThread.GetFrameAtIndex(2).Returns(mockFrame2);

            const uint maxStackDepth = 2;
            var framesInfo = remoteThread
                .GetFramesWithInfo(FrameInfoFlags.FIF_FUNCNAME, 0, maxStackDepth);

            Assert.AreEqual(maxStackDepth, framesInfo.Count);
            Assert.AreEqual("function0", framesInfo[0].Frame.GetFunctionName());
            Assert.AreEqual("function1", framesInfo[1].Frame.GetFunctionName());
        }
    }
}

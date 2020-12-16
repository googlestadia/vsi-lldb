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

﻿using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using YetiVSI.DebugEngine;
using YetiVSI.DebugEngine.AsyncOperations;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    public class FrameInfoEnumTest
    {
        const enum_FRAMEINFO_FLAGS _fieldSpec = enum_FRAMEINFO_FLAGS.FIF_ARGS_ALL;

        FrameInfoEnum _frameInfoEnum;
        StackFramesProvider _stackFramesProvider;
        IDebugThread _debugThread;

        [SetUp]
        public void Setup()
        {
            _stackFramesProvider =
                Substitute.ForPartsOf<StackFramesProvider>(null, null, null, null, null);
            _debugThread = Substitute.For<IDebugThread>();
            _frameInfoEnum = new FrameInfoEnum(
                _stackFramesProvider, _fieldSpec, _debugThread);
        }

        [Test]
        public void GetCount()
        {
            SetupActualNumberOfFrames(10);
            uint count;
            int result = _frameInfoEnum.GetCount(out count);
            _stackFramesProvider.Received(1).GetRange(_fieldSpec, _debugThread, 0, uint.MaxValue);
            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
            Assert.That(count, Is.EqualTo(10));
            uint numFetched = 0;
            FRAMEINFO[] results = new FRAMEINFO[1];
            _frameInfoEnum.Next(1, results, ref numFetched);
            Assert.That(results[0].m_bstrFuncName, Is.EqualTo("Func0"));
        }

        [Test]
        public void GetCountAfterNext()
        {
            SetupActualNumberOfFrames(20);
            uint numFetched = 0;
            FRAMEINFO[] results = new FRAMEINFO[10];
            _frameInfoEnum.Next(10, results, ref numFetched);
            _stackFramesProvider.ClearReceivedCalls();
            uint count;
            int result = _frameInfoEnum.GetCount(out count);
            _stackFramesProvider.Received(1).GetRange(_fieldSpec, _debugThread, 10, uint.MaxValue);
            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
            Assert.That(count, Is.EqualTo(20));
            numFetched = 0;
            results = new FRAMEINFO[1];
            _frameInfoEnum.Next(1, results, ref numFetched);
            Assert.That(results[0].m_bstrFuncName, Is.EqualTo("Func10"));
        }

        [Test]
        public void GetCountAfterAllLoaded()
        {
            SetupActualNumberOfFrames(20);
            uint numFetched = 0;
            FRAMEINFO[] results = new FRAMEINFO[21];
            _frameInfoEnum.Next(21, results, ref numFetched);
            _stackFramesProvider.ClearReceivedCalls();
            uint count;
            int result = _frameInfoEnum.GetCount(out count);
            _stackFramesProvider.DidNotReceive().GetRange(_fieldSpec, _debugThread,
                Arg.Any<uint>(), Arg.Any<uint>());
            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
            Assert.That(count, Is.EqualTo(20));
        }

        [Test]
        public void GetCountDoesntLoadDataMoreThanNecessary()
        {
            SetupActualNumberOfFrames(10);
            uint count1;
            int result1 = _frameInfoEnum.GetCount(out count1);
            uint count2;
            int result2 = _frameInfoEnum.GetCount(out count2);
            _stackFramesProvider.Received(1).GetRange(_fieldSpec, _debugThread, 0, uint.MaxValue);
            Assert.That(result1, Is.EqualTo(VSConstants.S_OK));
            Assert.That(count1, Is.EqualTo(10));
            Assert.That(result2, Is.EqualTo(VSConstants.S_OK));
            Assert.That(count2, Is.EqualTo(10));
        }

        [Test]
        public void Next()
        {
            SetupActualNumberOfFrames(10);
            uint numFetched = 0;
            FRAMEINFO[] results = new FRAMEINFO[1];
            int result = _frameInfoEnum.Next(1, results, ref numFetched);
            _stackFramesProvider.Received(1).GetRange(_fieldSpec, _debugThread, 0, 1);
            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
            Assert.That(numFetched, Is.EqualTo(1));
            Assert.That(results[0].m_bstrFuncName, Is.EqualTo("Func0"));
            _frameInfoEnum.Next(1, results, ref numFetched);
            Assert.That(results[0].m_bstrFuncName, Is.EqualTo("Func1"));
        }

        [Test]
        public void NextTwoTimes()
        {
            SetupActualNumberOfFrames(20);
            uint numFetched = 0;
            FRAMEINFO[] results = new FRAMEINFO[10];
            _frameInfoEnum.Next(5, results, ref numFetched);
            int result = _frameInfoEnum.Next(10, results, ref numFetched);
            _stackFramesProvider.Received(1).GetRange(_fieldSpec, _debugThread, 0, 5);
            _stackFramesProvider.Received(1).GetRange(_fieldSpec, _debugThread, 5, 10);
            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
            Assert.That(numFetched, Is.EqualTo(10));
            for(var i = 0; i < 10; ++i)
            {
                Assert.That(results[i].m_bstrFuncName, Is.EqualTo($"Func{5 + i}"));
            }
        }

        [Test]
        public void NextLoadAll()
        {
            SetupActualNumberOfFrames(10);
            uint numFetched = 0;
            FRAMEINFO[] results = new FRAMEINFO[10];
            int result = _frameInfoEnum.Next(10, results, ref numFetched);
            _stackFramesProvider.Received(1).GetRange(_fieldSpec, _debugThread, 0, 10);
            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
            Assert.That(numFetched, Is.EqualTo(10));
            for (var i = 0; i < 10; ++i)
            {
                Assert.That(results[i].m_bstrFuncName, Is.EqualTo($"Func{i}"));
            }
        }

        [Test]
        public void NextRequestedExceedsActual()
        {
            SetupActualNumberOfFrames(10);
            uint numFetched = 0;
            FRAMEINFO[] results = new FRAMEINFO[11];
            int result = _frameInfoEnum.Next(11, results, ref numFetched);
            Assert.That(result, Is.EqualTo(VSConstants.S_FALSE));
            Assert.That(numFetched, Is.EqualTo(10));
            for (var i = 0; i < 10; ++i)
            {
                Assert.That(results[i].m_bstrFuncName, Is.EqualTo($"Func{i}"));
            }
        }

        [Test]
        public void NextRequestedExceedsActualAfterGetCount()
        {
            SetupActualNumberOfFrames(10);
            uint count;
            _frameInfoEnum.GetCount(out count);
            uint numFetched = 0;
            FRAMEINFO[] results = new FRAMEINFO[11];
            int result = _frameInfoEnum.Next(11, results, ref numFetched);
            Assert.That(result, Is.EqualTo(VSConstants.S_FALSE));
            Assert.That(numFetched, Is.EqualTo(10));
            for (var i = 0; i < 10; ++i)
            {
                Assert.That(results[i].m_bstrFuncName, Is.EqualTo($"Func{i}"));
            }
        }

        [Test]
        public void NextSecondTimeExceedsActual()
        {
            SetupActualNumberOfFrames(10);
            uint numFetched = 0;
            FRAMEINFO[] results = new FRAMEINFO[10];
            _frameInfoEnum.Next(4, results, ref numFetched);
            int result = _frameInfoEnum.Next(7, results, ref numFetched);
            Assert.That(result, Is.EqualTo(VSConstants.S_FALSE));
            Assert.That(numFetched, Is.EqualTo(6));
            for (var i = 0; i < 6; ++i)
            {
                Assert.That(results[i].m_bstrFuncName, Is.EqualTo($"Func{4 + i}"));
            }
        }

        [Test]
        public void NextMaxCount()
        {
            SetupActualNumberOfFrames(10, true);
            _stackFramesProvider.MaxFramesNumberToLoad.Returns(11);
            uint numFetched = 0;
            FRAMEINFO[] results = new FRAMEINFO[11];
            int result = _frameInfoEnum.Next(11, results, ref numFetched);
            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
            Assert.That(numFetched, Is.EqualTo(11));
            Assert.That(results[10].m_fHasDebugInfo, Is.EqualTo(0));
            _stackFramesProvider.ClearReceivedCalls();
            result = _frameInfoEnum.Next(1, results, ref numFetched);
            Assert.That(result, Is.EqualTo(VSConstants.S_FALSE));
            Assert.That(numFetched, Is.EqualTo(0));
            _stackFramesProvider.DidNotReceive().GetRange(_fieldSpec, _debugThread,
                Arg.Any<uint>(), Arg.Any<uint>());
        }

        [Test]
        public void NextLoadsDataInLogTime()
        {
            SetupActualNumberOfFrames(1000);

            for(var i = 0; i < 100; ++i)
            {
                uint numFetched = 0;
                FRAMEINFO[] results = new FRAMEINFO[3];
                _frameInfoEnum.Next(3, results, ref numFetched);
            }

            _stackFramesProvider.Received(8).GetRange(_fieldSpec, _debugThread,
                Arg.Any<uint>(), Arg.Any<uint>());
        }

        [Test]
        public void Reset()
        {
            SetupActualNumberOfFrames(10);
            uint numFetched = 0;
            FRAMEINFO[] results = new FRAMEINFO[10];
            _frameInfoEnum.Next(4, results, ref numFetched);
            _stackFramesProvider.ClearReceivedCalls();
            int result = _frameInfoEnum.Reset();
            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
            _frameInfoEnum.Next(5, results, ref numFetched);
            Assert.That(results[0].m_bstrFuncName, Is.EqualTo("Func0"));
            _stackFramesProvider.Received(1).GetRange(_fieldSpec, _debugThread,
                4, Arg.Is<uint>(x => x >= 4));
        }

        [Test]
        public void ResetAfterAllLoaded()
        {
            SetupActualNumberOfFrames(10);
            uint numFetched = 0;
            FRAMEINFO[] results = new FRAMEINFO[10];
            _frameInfoEnum.Next(10, results, ref numFetched);
            _stackFramesProvider.ClearReceivedCalls();
            int result = _frameInfoEnum.Reset();
            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
            _frameInfoEnum.Next(1, results, ref numFetched);
            Assert.That(results[0].m_bstrFuncName, Is.EqualTo("Func0"));
            _stackFramesProvider.DidNotReceive().GetRange(_fieldSpec, _debugThread,
                Arg.Any<uint>(), Arg.Any<uint>());
        }

        [Test]
        public void Skip()
        {
            SetupActualNumberOfFrames(20);
            int result = _frameInfoEnum.Skip(5);
            _stackFramesProvider.Received(1).GetRange(_fieldSpec, _debugThread, 0, 5);
            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
            uint numFetched = 0;
            FRAMEINFO[] results = new FRAMEINFO[1];
            _frameInfoEnum.Next(1, results, ref numFetched);
            Assert.That(results[0].m_bstrFuncName, Is.EqualTo("Func5"));
        }

        [Test]
        public void SkipAfterNext()
        {
            SetupActualNumberOfFrames(20);
            uint numFetched = 0;
            FRAMEINFO[] results = new FRAMEINFO[5];
            _frameInfoEnum.Next(5, results, ref numFetched);
            int result = _frameInfoEnum.Skip(2);
            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
            _frameInfoEnum.Next(1, results, ref numFetched);
            Assert.That(results[0].m_bstrFuncName, Is.EqualTo("Func7"));
        }

        [Test]
        public void ResetAfterSkip()
        {
            SetupActualNumberOfFrames(20);
            _frameInfoEnum.Skip(7);
            _frameInfoEnum.Reset();
            uint numFetched = 0;
            FRAMEINFO[] results = new FRAMEINFO[5];
            _frameInfoEnum.Next(1, results, ref numFetched);
            Assert.That(results[0].m_bstrFuncName, Is.EqualTo("Func0"));
        }

        [Test]
        public void SkipExceedsAll()
        {
            SetupActualNumberOfFrames(20);
            int result = _frameInfoEnum.Skip(21);
            _stackFramesProvider.Received(1).GetRange(_fieldSpec, _debugThread, 0, 21);
            Assert.That(result, Is.EqualTo(VSConstants.S_FALSE));
        }

        /// <summary>
        /// Regression test for (internal).
        /// </summary>
        [Test]
        public void GetFramesWithNoDebugInfoFrame()
        {
            _stackFramesProvider.GetRange(Arg.Any<enum_FRAMEINFO_FLAGS>(), Arg.Any<IDebugThread>(),
                0, 1).Returns(new List<FRAMEINFO>
                {
                    new FRAMEINFO { m_bstrFuncName = $"libFunc()", m_fHasDebugInfo = 0 }
                });
            _stackFramesProvider.GetRange(Arg.Any<enum_FRAMEINFO_FLAGS>(), Arg.Any<IDebugThread>(),
                1, 1).Returns(new List<FRAMEINFO>
            {
                new FRAMEINFO { m_bstrFuncName = $"mainFunc()", m_fHasDebugInfo = 1 }
            });
            uint numFetched = 0;
            FRAMEINFO[] results = new FRAMEINFO[1];
            _frameInfoEnum.Next(1, results, ref numFetched);

            int result = _frameInfoEnum.Next(1, results, ref numFetched);
            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
            Assert.That(numFetched, Is.EqualTo(1));
            Assert.That(results[0].m_bstrFuncName, Is.EqualTo("mainFunc()"));
        }

        void SetupActualNumberOfFrames(uint number, bool appendMaxExceededInfoEntry = false)
        {
            _stackFramesProvider.GetRange(Arg.Any<enum_FRAMEINFO_FLAGS>(), Arg.Any<IDebugThread>(),
                Arg.Any<uint>(), Arg.Any<uint>()).Returns(args =>
                {
                    uint startIndex = (uint)args[2];
                    uint count = (uint)args[3];
                    var list = new List<FRAMEINFO>();
                    for (var i = startIndex; i < Math.Min(number, (long)startIndex + count); ++i)
                    {
                        list.Add(
                            new FRAMEINFO { m_bstrFuncName = $"Func{i}", m_fHasDebugInfo = 1 });
                    }

                    if (appendMaxExceededInfoEntry)
                    {
                        list.Add(new FRAMEINFO { m_fHasDebugInfo = 0,
                            m_bstrFuncName = "Max number of frames exceeded"
                        });
                    }

                    return list;
                });
        }
    }
}

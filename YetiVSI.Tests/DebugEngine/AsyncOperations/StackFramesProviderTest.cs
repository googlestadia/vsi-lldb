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
using DebuggerCommonApi;
using DebuggerGrpcClient.Interfaces;
using Microsoft.VisualStudio.Debugger.Interop;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YetiVSI.DebugEngine;
using YetiVSI.DebugEngine.AsyncOperations;

namespace YetiVSI.Test.DebugEngine.AsyncOperations
{
    [TestFixture]
    public class StackFramesProviderTest
    {
        const enum_FRAMEINFO_FLAGS _fieldSpec = enum_FRAMEINFO_FLAGS.FIF_ARGS_ALL;

        StackFramesProvider _stackFramesProvider;
        RemoteThread _remoteThread;
        IGgpDebugProgram _debugProgram;
        AD7FrameInfoCreator _ad7FrameInfoCreator;
        IDebugThread _debugThread;

        [SetUp]
        public void SetUp()
        {
            _remoteThread = Substitute.For<RemoteThread>();
            _debugProgram = Substitute.For<IGgpDebugProgram>();
            _ad7FrameInfoCreator = Substitute.For<AD7FrameInfoCreator>(
                Substitute.For<IDebugModuleCache>());
            _ad7FrameInfoCreator
                .Create(Arg.Any<IDebugStackFrame>(), Arg.Any<FrameInfo<SbModule>>(), _debugProgram)
                .ReturnsForAnyArgs(args => CreateFRAMEINFO((IDebugStackFrame)args[0],
                                                           (FrameInfo<SbModule>)args[1]));
            _debugThread = Substitute.For<IDebugThread>();
            _stackFramesProvider =
                new StackFramesProvider(_remoteThread, CreateStackFrame, _debugProgram,
                                        _ad7FrameInfoCreator, _debugProgram);
        }

        [Test]
        public async Task GetAsyncOneFrameAsync()
        {
            SetupActualNumberOfFrames(1);

            IList<FRAMEINFO> frames =
                await _stackFramesProvider.GetAllAsync(_fieldSpec, _debugThread);

            await _remoteThread.Received(1).GetFramesWithInfoAsync(
                (FrameInfoFlags) _fieldSpec, 0, StackFramesProvider.FramesBatchSize);
            Assert.IsNotNull(frames);
            Assert.That(frames.Count, Is.EqualTo(1));
            Assert.That(frames[0].m_bstrFuncName, Is.EqualTo("Func0"));
        }

        [Test]
        public async Task GetAsyncInTwoBatchesAsync()
        {
            SetupActualNumberOfFrames(StackFramesProvider.FramesBatchSize + 5);

            IList<FRAMEINFO> frames =
                await _stackFramesProvider.GetAllAsync(_fieldSpec, _debugThread);

            Received.InOrder(() =>
            {
                _remoteThread.GetFramesWithInfoAsync(
                    (FrameInfoFlags) _fieldSpec, 0, StackFramesProvider.FramesBatchSize).Wait();
                _remoteThread.GetFramesWithInfoAsync((FrameInfoFlags)_fieldSpec,
                                                     StackFramesProvider.FramesBatchSize,
                                                     StackFramesProvider.FramesBatchSize).Wait();
            });
            Assert.IsNotNull(frames);
            Assert.That(frames.Count, Is.EqualTo(205));
            for (var i = 0; i < StackFramesProvider.FramesBatchSize + 5; ++i)
            {
                Assert.That(frames[i].m_bstrFuncName, Is.EqualTo($"Func{i}"));
            }
        }

        [Test]
        public async Task GetAsyncMaxNumberOfFramesAsync()
        {
            SetupActualNumberOfFrames(StackFramesProvider.MaxStackDepth);

            IList<FRAMEINFO> frames =
                await _stackFramesProvider.GetAllAsync(_fieldSpec, _debugThread);

            Assert.IsNotNull(frames);
            Assert.That(frames.Count, Is.EqualTo(StackFramesProvider.MaxStackDepth));
            Assert.That(frames[0].m_bstrFuncName, Is.EqualTo("Func0"));
            Assert.That(frames[(int) StackFramesProvider.MaxStackDepth / 3].m_bstrFuncName,
                        Is.EqualTo($"Func{StackFramesProvider.MaxStackDepth / 3}"));
            Assert.That(frames[(int) StackFramesProvider.MaxStackDepth - 1].m_bstrFuncName,
                        Is.EqualTo($"Func{StackFramesProvider.MaxStackDepth - 1}"));
            Assert.That(frames[(int) StackFramesProvider.MaxStackDepth - 1].m_fHasDebugInfo,
                        Is.EqualTo(1));
        }

        [Test]
        public async Task GetAsyncMoreThanMaxNumberOfFramesAsync()
        {
            SetupActualNumberOfFrames(StackFramesProvider.MaxStackDepth + 5);

            IList<FRAMEINFO> frames =
                await _stackFramesProvider.GetAllAsync(_fieldSpec, _debugThread);

            Assert.IsNotNull(frames);
            Assert.That(frames.Count, Is.EqualTo(StackFramesProvider.MaxStackDepth + 1));
            Assert.That(frames[0].m_bstrFuncName, Is.EqualTo("Func0"));
            Assert.That(frames[(int) StackFramesProvider.MaxStackDepth - 1].m_bstrFuncName,
                        Is.EqualTo($"Func{StackFramesProvider.MaxStackDepth - 1}"));
            Assert.That(frames[(int) StackFramesProvider.MaxStackDepth].m_bstrFuncName
                            .Contains("has been exceeded"));
            Assert.That(frames[(int) StackFramesProvider.MaxStackDepth].m_fHasDebugInfo,
                        Is.EqualTo(0));
            Assert.That(frames[(int) StackFramesProvider.MaxStackDepth].m_dwValidFields,
                        Is.EqualTo(enum_FRAMEINFO_FLAGS.FIF_FUNCNAME |
                                   enum_FRAMEINFO_FLAGS.FIF_DEBUGINFO));
        }

        [Test]
        public void GetRangeFirstFrame()
        {
            SetupActualNumberOfFrames(5);

            IList<FRAMEINFO> frames = _stackFramesProvider.GetRange(_fieldSpec, _debugThread, 0, 1);

            _remoteThread.Received(1).GetFramesWithInfo((FrameInfoFlags) _fieldSpec, 0, 1);
            Assert.IsNotNull(frames);
            Assert.That(frames.Count, Is.EqualTo(1));
            Assert.That(frames[0].m_bstrFuncName, Is.EqualTo("Func0"));
        }

        [Test]
        public void GetRangeFromInside()
        {
            SetupActualNumberOfFrames(15);

            IList<FRAMEINFO> frames = _stackFramesProvider.GetRange(_fieldSpec, _debugThread, 4, 7);

            _remoteThread.Received(1).GetFramesWithInfo((FrameInfoFlags) _fieldSpec, 4, 7);
            Assert.IsNotNull(frames);
            Assert.That(frames.Count, Is.EqualTo(7));
            for (var i = 4; i < 11; ++i)
            {
                Assert.That(frames[i - 4].m_bstrFuncName, Is.EqualTo($"Func{i}"));
            }
        }

        [Test]
        public void GetRangeInTheEnd()
        {
            SetupActualNumberOfFrames(9);

            IList<FRAMEINFO> frames = _stackFramesProvider.GetRange(_fieldSpec, _debugThread, 4, 7);

            _remoteThread.Received(1).GetFramesWithInfo((FrameInfoFlags) _fieldSpec, 4, 7);
            Assert.IsNotNull(frames);
            Assert.That(frames.Count, Is.EqualTo(5));
            for (var i = 4; i < 9; ++i)
            {
                Assert.That(frames[i - 4].m_bstrFuncName, Is.EqualTo($"Func{i}"));
            }
        }

        [Test]
        public void GetRangeExceedMaxNumber()
        {
            SetupActualNumberOfFrames(StackFramesProvider.MaxStackDepth + 10);
            uint startIndex = StackFramesProvider.MaxStackDepth - 5;

            IList<FRAMEINFO> frames = _stackFramesProvider.GetRange(
                _fieldSpec, _debugThread, startIndex, 10);

            _remoteThread.Received(1).GetFramesWithInfo((FrameInfoFlags) _fieldSpec, startIndex, 6);
            Assert.IsNotNull(frames);
            Assert.That(frames.Count, Is.EqualTo(6));
            for (var i = startIndex; i < StackFramesProvider.MaxStackDepth; ++i)
            {
                Assert.That(frames[(int) (i - startIndex)].m_bstrFuncName, Is.EqualTo($"Func{i}"));
            }

            Assert.That(frames[5].m_bstrFuncName.Contains("has been exceeded"));
            Assert.That(frames[5].m_fHasDebugInfo, Is.EqualTo(0));
            Assert.That(frames[5].m_dwValidFields,
                        Is.EqualTo(enum_FRAMEINFO_FLAGS.FIF_FUNCNAME |
                                   enum_FRAMEINFO_FLAGS.FIF_DEBUGINFO));
        }

        [Test]
        public void GetRangeUpToMaxNumber()
        {
            SetupActualNumberOfFrames(StackFramesProvider.MaxStackDepth + 10);
            uint startIndex = StackFramesProvider.MaxStackDepth - 5;

            IList<FRAMEINFO> frames = _stackFramesProvider.GetRange(
                _fieldSpec, _debugThread, startIndex, 5);

            _remoteThread.Received(1).GetFramesWithInfo((FrameInfoFlags) _fieldSpec, startIndex, 5);
            Assert.IsNotNull(frames);
            Assert.That(frames.Count, Is.EqualTo(5));
            for (var i = startIndex; i < StackFramesProvider.MaxStackDepth; ++i)
            {
                Assert.That(frames[(int) (i - startIndex)].m_bstrFuncName, Is.EqualTo($"Func{i}"));
            }
        }

        [Test]
        public void GetRangeExceedMaxNumberActualNumberDoesNotExceed()
        {
            SetupActualNumberOfFrames(StackFramesProvider.MaxStackDepth);
            uint startIndex = StackFramesProvider.MaxStackDepth - 5;

            IList<FRAMEINFO> frames = _stackFramesProvider.GetRange(
                _fieldSpec, _debugThread, startIndex, 10);

            _remoteThread.Received(1).GetFramesWithInfo((FrameInfoFlags) _fieldSpec, startIndex, 6);
            Assert.IsNotNull(frames);
            Assert.That(frames.Count, Is.EqualTo(5));
            for (var i = startIndex; i < StackFramesProvider.MaxStackDepth; ++i)
            {
                Assert.That(frames[(int) (i - startIndex)].m_bstrFuncName, Is.EqualTo($"Func{i}"));
            }
        }

        [Test]
        public void GetRangeUpToMaxUint()
        {
            SetupActualNumberOfFrames(10);

            IList<FRAMEINFO> frames = _stackFramesProvider.GetRange(
                _fieldSpec, _debugThread, 0, uint.MaxValue);

            _remoteThread.Received(1).GetFramesWithInfo(
                (FrameInfoFlags) _fieldSpec, 0, StackFramesProvider.MaxStackDepth + 1);
            Assert.IsNotNull(frames);
            Assert.That(frames.Count, Is.EqualTo(10));
        }

        void SetupActualNumberOfFrames(uint number)
        {
            _remoteThread
                .GetFramesWithInfoAsync(Arg.Any<FrameInfoFlags>(), Arg.Any<uint>(), Arg.Any<uint>())
                .ReturnsForAnyArgs(
                    args => Task.FromResult(
                        GetFramesWithInfo((uint) args[1], (uint) args[2], number)));
            _remoteThread
                .GetFramesWithInfo(Arg.Any<FrameInfoFlags>(), Arg.Any<uint>(), Arg.Any<uint>())
                .ReturnsForAnyArgs(
                    args => GetFramesWithInfo((uint) args[1], (uint) args[2], number));
        }

        List<FrameInfoPair> GetFramesWithInfo(uint startIndex, uint maxCount, uint actualCount)
        {
            var result = new List<FrameInfoPair>();
            for (uint i = startIndex; i < Math.Min(startIndex + maxCount, actualCount); ++i)
            {
                result.Add(new FrameInfoPair(Substitute.For<RemoteFrame>(),
                                             new FrameInfo<SbModule>
                                                 {FuncName = $"Func{i}", HasDebugInfo = 1}));
            }

            return result;
        }

        IDebugStackFrame CreateStackFrame(RemoteFrame frame, IDebugThread thread,
                                          IGgpDebugProgram debugProgram)
        {
            return Substitute.For<IDebugStackFrame>();
        }

        FRAMEINFO CreateFRAMEINFO(IDebugStackFrame frame, FrameInfo<SbModule> info) =>
            new FRAMEINFO
            {
                m_bstrFuncName = info.FuncName,
                m_fHasDebugInfo = info.HasDebugInfo,
                m_dwValidFields = (enum_FRAMEINFO_FLAGS) info.ValidFields,
                m_pFrame = (FrameInfoFlags.FIF_FRAME & info.ValidFields) != 0 ? frame : null
            };
    }
}
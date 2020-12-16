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

﻿using Debugger.Common;
using DebuggerCommonApi;
using DebuggerGrpcClient;
using DebuggerGrpcServer;
using DebuggerGrpcServer.RemoteInterfaces;
using LldbApi;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace DebuggerGrpcClientServer.Tests
{
    // These tests require x64 test architecture. To run them locally, set
    //   Test > Test Settings > DefaultArchitecture > x64
    // and recompile.

    [TestFixture]
    [Timeout(5000)]
    class RemoteThreadIntegrationTests : BaseIntegrationTests
    {
        const uint _maxStackDepth = 5;
        const FrameInfoFlags _emptyFrameInfoFlags = 0;

        RemoteThread mockRemoteThread;
        DebuggerApi.RemoteThread threadProxy;

        [SetUp]
        public void SetUp()
        {
            BaseSetUp();
            mockRemoteThread = Substitute.For<RemoteThread>();

            var grpcThreadFactory = new GrpcThreadFactory();
            threadProxy = grpcThreadFactory.Create(Connection, new GrpcSbThread
            {
                Id = ThreadStore.AddObject(mockRemoteThread)
            });
        }

        [TearDown]
        public void TearDown()
        {
            ((IDisposable)threadProxy).Dispose();
            BaseTearDown();
        }

        [Test]
        public void GetFramesWithInfoReturnsEmptyList()
        {
            mockRemoteThread
                .GetFramesWithInfo(_emptyFrameInfoFlags, 0, _maxStackDepth)
                .Returns(new List<FrameInfoPair>());

            List<DebuggerGrpcClient.Interfaces.FrameInfoPair> framesWithInfo =
                threadProxy.GetFramesWithInfo(_emptyFrameInfoFlags, 0, _maxStackDepth);
            Assert.IsEmpty(framesWithInfo);
        }

        [Test]
        public void GetFramesWithInfoReturnsModulePointer([Values(false, true)] bool nullModule)
        {
            const string moduleUUID = "12345678-1234-1234-1234-123456789123";
            const string frame1Name = "frame1";

            var module = nullModule ? null : Substitute.For<SbModule>();
            module?.GetUUIDString().Returns(moduleUUID);
            var frame = Substitute.For<RemoteFrame>();
            frame.GetFunctionName().Returns(frame1Name);
            var frameInfoPair = new FrameInfoPair
            {
                Frame = frame,
                Info = new FrameInfo<SbModule> { Module = module }
            };

            mockRemoteThread
                .GetFramesWithInfo(FrameInfoFlags.FIF_DEBUG_MODULEP, 0, _maxStackDepth)
                .Returns(new List<FrameInfoPair> { frameInfoPair });

            List<DebuggerGrpcClient.Interfaces.FrameInfoPair> framesWithInfo =
                threadProxy.GetFramesWithInfo(FrameInfoFlags.FIF_DEBUG_MODULEP, 0, _maxStackDepth);
            Assert.AreEqual(1, framesWithInfo.Count);
            Assert.AreEqual(frame1Name, framesWithInfo[0].Frame.GetFunctionName());
            if (nullModule)
            {
                Assert.IsNull(framesWithInfo[0].Info.Module);
            }
            else
            {
                Assert.AreEqual(moduleUUID, framesWithInfo[0].Info.Module.GetUUIDString());
            }

            ((IDisposable)framesWithInfo[0].Info.Module)?.Dispose();
            ((IDisposable)framesWithInfo[0].Frame).Dispose();
        }
    }
}

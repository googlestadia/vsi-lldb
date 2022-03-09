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
using DebuggerGrpcClient;
using Microsoft.VisualStudio.Threading;
using NUnit.Framework;
using System;

namespace YetiVSI.Test.TestSupport.Lldb
{
    [TestFixture]
    class SbPlatformFakeTests
    {
        SbPlatform platform;
        SbPlatformConnectOptions connectOptions;
        GrpcPlatformShellCommandFactory shellCommandFactory;

        [SetUp]
        public void SetUp()
        {
            var factory = new GrpcPlatformFactoryFake(null);
            factory.AddFakeProcess("linux-remote", "myGame", 2222);
            factory.AddFakeProcess("linux-remote", "ssh", 443244);
            factory.AddFakeProcess("linux-remote", "blah", 4545);

            var callInvokerFactory = new PipeCallInvokerFactory();
            var grpcConnection =
#pragma warning disable VSSDK005 // Avoid instantiating JoinableTaskContext
                new GrpcConnection(new JoinableTaskContext().Factory, callInvokerFactory.Create());
#pragma warning restore VSSDK005 // Avoid instantiating JoinableTaskContext
            platform = factory.Create("linux-remote", grpcConnection);
            connectOptions = new GrpcPlatformConnectOptionsFactory()
                .Create("http://any/url");
            shellCommandFactory = new GrpcPlatformShellCommandFactory();
        }

        [Test]
        public void ConnectRemoteReturnsSuccess()
        {
            var result = platform.ConnectRemote(connectOptions);
            Assert.That(result.Success(), Is.True);
        }

        [Test]
        public void TestRunCommandWithUnknownCommand()
        {
            Assert.Throws<NotSupportedException>(() => platform.Run(
                shellCommandFactory.Create("awk blah")));
        }

        [Test]
        public void TestRunPidCommandWithUnknownProcess()
        {
            var result = platform.Run(shellCommandFactory.Create("pidof \"test.exe\""));
            Assert.That(result.Success(), Is.False);
            Assert.That(result.GetCString(), Does.Contain("unknown process"));
            Assert.That(result.GetCString(), Does.Contain("test.exe"));
        }

        [Test, Sequential]
        public void TestRunPidCommand([Values("myGame", "ssh", "blah")] string process,
            [Values(2222, 443244, 4545)] int pid)
        {
            var cmd = shellCommandFactory.Create($"pidof \"{process}\"");
            var result = platform.Run(cmd);
            Assert.That(result.Success(), Is.True);
            Assert.That(cmd.GetOutput(), Is.EqualTo(pid.ToString()));
        }
    }
}

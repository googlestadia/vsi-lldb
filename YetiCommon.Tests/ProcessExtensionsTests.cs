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

﻿using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
using TestsCommon.TestSupport;

namespace YetiCommon.Tests
{
    [TestFixture]
    public class ProcessExtensionsTests
    {
        const int FakePid = 1234;
        const string FakeFilename = @"C:\src\test.exe";

        IProcess process;

        [SetUp]
        public void SetUp()
        {
            process = Substitute.For<IProcess>();
            process.Id.Returns(FakePid);
            process.ProcessName.Returns(FakeFilename);
        }

        [Test]
        public void RunToExitWithSuccessAsync_Success()
        {
            process.RunToExitAsync().Returns(0);
            Assert.DoesNotThrowAsync(async () => await process.RunToExitWithSuccessAsync());
        }

        [Test]
        public void RunToExitWithSuccessAsync_Failure()
        {
            var exitCode = 1;
            process.RunToExitAsync().Returns(exitCode);
            var ex = Assert.ThrowsAsync<ProcessExecutionException>(
                async () => await process.RunToExitWithSuccessAsync());

            Assert.That(ex, Has.Message.Contain(FakeFilename));
            Assert.That(ex, Has.Message.Contain(exitCode.ToString()));
        }

        [Test]
        public void RunToExitWithSuccessAsync_Exception()
        {
            process.When(x => x.RunToExitAsync()).Do(x =>
            {
                throw new ProcessException("test exception");
            });

            Assert.ThrowsAsync<ProcessException>(
                async () => await process.RunToExitWithSuccessAsync());
        }

        [Test]
        public async Task RunToExitWithSuccessCapturingOutputAsync_SuccessAsync()
        {
            process.When(x => x.RunToExitAsync()).Do(x =>
            {
                OutputTestData(process, "output text", "");
            });
            var output = await process.RunToExitWithSuccessCapturingOutputAsync();
            Assert.That(output, Has.Count.EqualTo(1));
            Assert.That(output[0], Is.EqualTo("output text"));
        }

        [Test]
        public void RunToExitWithSuccessCapturingOutputAsync_Failure()
        {
            var exitCode = 1;
            process.RunToExitAsync().Returns(exitCode).AndDoes(x =>
            {
                OutputTestData(process, "output text", "error text");
            });
            var ex = Assert.ThrowsAsync<ProcessExecutionException>(
                async () => await process.RunToExitWithSuccessCapturingOutputAsync());

            Assert.That(ex, Has.Message.Contain(FakeFilename));
            Assert.That(ex, Has.Message.Contain(exitCode.ToString()));
            Assert.That(ex.OutputLines, Has.Count.EqualTo(1));
            Assert.That(ex.OutputLines[0], Is.EqualTo("output text"));
            Assert.That(ex.ErrorLines, Has.Count.EqualTo(1));
            Assert.That(ex.ErrorLines[0], Is.EqualTo("error text"));
        }

        [Test]
        public void RunToExitWithSuccesssCapturingOutputAsync_Exception()
        {
            process.When(x => x.RunToExitAsync()).Do(x =>
            {
                throw new ProcessException("test exception");
            });

            Assert.ThrowsAsync<ProcessException>(
                async () => await process.RunToExitWithSuccessCapturingOutputAsync());
        }

        void OutputTestData(IProcess process, string output, string error)
        {
           process.OutputDataReceived +=
                Raise.Event<TextReceivedEventHandler>(this, new TextReceivedEventArgs(output));
            process.OutputDataReceived +=
                Raise.Event<TextReceivedEventHandler>(this, new TextReceivedEventArgs(null));
            process.ErrorDataReceived +=
                Raise.Event<TextReceivedEventHandler>(this, new TextReceivedEventArgs(error));
            process.ErrorDataReceived +=
                Raise.Event<TextReceivedEventHandler>(this, new TextReceivedEventArgs(null));
        }
    }
}

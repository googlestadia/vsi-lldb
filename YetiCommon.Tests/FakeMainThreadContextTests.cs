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

﻿using NUnit.Framework;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

namespace YetiCommon.Tests
{
    [TestFixture]
    class FakeMainThreadContextTests
    {
        FakeMainThreadContext mainThreadContext;
        JoinableTaskContext taskContext;
        JoinableTaskFactory taskFactory;

        [SetUp]
        public void SetUp()
        {
            mainThreadContext = new FakeMainThreadContext();
            taskContext = mainThreadContext.JoinableTaskContext;
            taskFactory = taskContext.Factory;
        }

        [TearDown]
        public void TearDown()
        {
            mainThreadContext.Dispose();
        }

        [Test]
        public void NotOnMainThread()
        {
            Assert.False(taskContext.IsOnMainThread);
        }

        [Test]
        public async Task SwitchToMainThreadAsync()
        {
            await taskFactory.SwitchToMainThreadAsync();

            Assert.True(taskContext.IsOnMainThread);
        }

        [Test]
        public async Task RemainOnMainThreadAsync()
        {
            await taskFactory.SwitchToMainThreadAsync();
            await Task.Yield();

            Assert.True(taskContext.IsOnMainThread);
        }

        [Test]
        public async Task SwitchToWorkerThreadAsync()
        {
            await taskFactory.SwitchToMainThreadAsync();
            await TaskScheduler.Default;

            Assert.False(taskContext.IsOnMainThread);
        }

        [Test]
        public async Task RunRemainsOnMainThreadAsync()
        {
            await taskFactory.SwitchToMainThreadAsync();
            // Disable "Run synchronously blocks. Await RunAsync instead".
            // The point is to test that taskFactory.Run() remains on the main thread.
#pragma warning disable VSTHRD103
            taskFactory.Run(async () =>
            {
                await Task.Yield();

                Assert.True(taskContext.IsOnMainThread);
            });
#pragma warning restore VSTHRD103
        }

        [Test]
        public void RunAndSwitchToMainThread()
        {
            taskFactory.Run(async () =>
            {
                await taskFactory.SwitchToMainThreadAsync();

                Assert.True(taskContext.IsOnMainThread);
            });
        }

        [Test]
        public async Task RunAndSwitchToWorkerThreadAsync()
        {
            await taskFactory.SwitchToMainThreadAsync();
            // Disable "Run synchronously blocks. Await RunAsync instead".
            // The point is to test taskFactory.Run().
#pragma warning disable VSTHRD103
            taskFactory.Run(async () =>
            {
                await TaskScheduler.Default;

                Assert.False(taskContext.IsOnMainThread);
            });
#pragma warning restore VSTHRD103
        }
    }
}

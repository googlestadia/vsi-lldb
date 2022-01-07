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

﻿using Google.VisualStudioFake.Util;
using Microsoft.VisualStudio.Threading;
using NUnit.Framework;
using System;
// TODO: VSFake project should not depend on any Yeti projects.
using YetiCommon;

namespace Google.Tests.VisualStudioFake.Util
{
    [TestFixture]
    public class JoinableTaskContextExtensionsTests
    {
        class TestException : Exception { }

        FakeMainThreadContext _mainThreadContext;
        JoinableTaskContext _taskContext;

        [SetUp]
        public void Setup()
        {
            _mainThreadContext = new FakeMainThreadContext();
            _taskContext = _mainThreadContext.JoinableTaskContext;
        }

        [TearDown]
        public void TearDown()
        {
            _mainThreadContext.Dispose();
        }

        [Test]
        public void RunOnMainThread()
        {
            Assert.Throws<TestException>(() => _taskContext.RunOnMainThread(() =>
            {
                Assert.That(_taskContext.IsOnMainThread, Is.True);
                throw new TestException();
            }));
        }
    }
}

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

namespace TestsCommon.TestSupport
{
    [TestFixture]
    class LogSpyTests
    {
        LogSpy logSpy;

        [SetUp]
        public void SetUp()
        {
            logSpy = new LogSpy();
        }

        [Test]
        public void GetOutputNoLimitNoOffset()
        {
            logSpy.Write("This is a test");
            Assert.That(logSpy.GetOutput(0, -1), Is.EqualTo("This is a test"));
        }

        [Test]
        public void GetOutputLimitNoOffset()
        {
            logSpy.WriteLine("This is a test");
            Assert.That(logSpy.GetOutput(0, 3), Is.EqualTo("Thi"));
        }

        [Test]
        public void GetOutputNoLimitOffset()
        {
            logSpy.Write("This is a test");
            Assert.That(logSpy.GetOutput(3, -1), Is.EqualTo("s is a test"));
        }

        [Test]
        public void GetOutputNegativeOffset()
        {
            logSpy.Write("This is a test");
            Assert.That(logSpy.GetOutput(-3, -1), Is.EqualTo("est"));
        }

        [Test]
        public void GetOuputNegativeOffsetMoreThanTotalSize()
        {
            logSpy.Write("This is a test");
            Assert.That(logSpy.GetOutput(-50 , -1), Is.EqualTo("This is a test"));
        }
    }
}

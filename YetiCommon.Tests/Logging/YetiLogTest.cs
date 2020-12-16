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
using System;
using YetiCommon.Logging;

namespace YetiCommon.Tests.Logging
{
    [TestFixture]
    class LoggingTest
    {
        [SetUp]
        public void SetUp()
        {
            YetiLog.Initialize(nameof(LoggingTest), DateTime.Now);
        }

        [TearDown]
        public void TearDown()
        {
            YetiLog.Uninitialize();
        }

        [Test]
        public void TestGetTraceLogger()
        {
            Assert.Throws<ArgumentException>(() => YetiLog.GetTraceLogger(null));
            Assert.Throws<ArgumentException>(() => YetiLog.GetTraceLogger(""));
            Assert.Throws<ArgumentException>(() => YetiLog.GetTraceLogger("foo/bar"));
            Assert.Throws<ArgumentException>(() => YetiLog.GetTraceLogger("foo.bar"));

            var logger = YetiLog.GetTraceLogger(YetiLog.ToLogFileDateTime(DateTime.Now));
            Assert.IsNotNull(logger);
        }
    }
}

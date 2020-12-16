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
using NSubstitute;
using System;
using YetiCommon.ExceptionRecorder;
using System.Reflection;

namespace YetiCommon.Tests.ExceptionRecorder
{
    public class ExceptionRecorderExtensionsTests
    {
        Exception fakeException;
        IExceptionRecorder mockRecorder;

        [SetUp]
        public void SetUp()
        {
            fakeException = new TestException();
            mockRecorder = Substitute.For<IExceptionRecorder>();
        }

        [Test]
        public void RecordHere()
        {
            ExceptionRecorderExtensions.RecordHere(mockRecorder, fakeException);
            mockRecorder.Received().Record(MethodBase.GetCurrentMethod(), fakeException);
        }

        [Test]
        public void RecordHereNullExceptionFails()
        {
            Assert.Catch<ArgumentNullException>(
                () => ExceptionRecorderExtensions.RecordHere(mockRecorder, null));
        }

        [Test]
        public void SafelyRecordHere()
        {
            ExceptionRecorderExtensions.SafelyRecordHere(mockRecorder, fakeException);
            mockRecorder.Received().Record(MethodBase.GetCurrentMethod(), fakeException);
        }

        [Test]
        public void SafelyRecordHereSwallowsException()
        {
            mockRecorder.WhenForAnyArgs(x => x.Record(null, null)).Throw<RecordException>();

            Assert.DoesNotThrow(
                () => ExceptionRecorderExtensions.SafelyRecordHere(mockRecorder, fakeException));
        }

        [Test]
        public void SafelyRecordHereNullException()
        {
            Assert.DoesNotThrow(
                () => ExceptionRecorderExtensions.SafelyRecordHere(mockRecorder, null));
        }

        class TestException : Exception { }
        class RecordException : Exception { }
    }
}

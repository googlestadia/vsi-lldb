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
using YetiVSI.DebugEngine.Exit;

namespace YetiVSI.Test.DebugEngine.Exit
{
    [TestFixture]
    class ExitInfoTests
    {
        [Test]
        public void IfErrorWhenNormal()
        {
            var ei = ExitInfo.Normal(ExitReason.Unknown);

            var isCalled = false;
            Action<Exception> a = ex =>
            {
                isCalled = true;
            };
            ei.IfError(a);

            Assert.IsFalse(isCalled);
        }

        [Test]
        public void IfErrorWhenError()
        {
            var expected = new Exception();
            var ei = ExitInfo.Error(expected);

            Exception actual = null;
            ei.IfError(e => actual = e);

            Assert.AreEqual(expected, actual);
        }

        [TestCaseSource(typeof(Enum), "GetValues", new object[] { typeof(ExitReason) })]
        public void HandleResultWhenNormal(ExitReason expected)
        {
            var ei = ExitInfo.Normal(expected);

            ExitReason? actual = null;
            Exception error = null;

            ei.HandleResult(
                onNormal: r => actual = r,
                onError: ex => error = ex); ;

            Assert.AreEqual(expected, actual);
            Assert.IsNull(error);
        }

        [Test]
        public void HandleResultWhenError()
        {
            var expected = new Exception();
            var ei = ExitInfo.Error(expected);

            Exception actual = null;
            var normalCalled = false;

            ei.HandleResult(
                onNormal: _ => normalCalled = true,
                onError: e => actual = e);

            Assert.AreEqual(expected, actual);
            Assert.IsFalse(normalCalled);
        }
    }
}

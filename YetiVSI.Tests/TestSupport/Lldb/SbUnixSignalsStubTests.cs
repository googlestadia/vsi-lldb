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

using NUnit.Framework;
using System;

namespace YetiVSI.Test.TestSupport.Lldb
{
    [TestFixture]
    class SbUnixSignalsStubTests
    {
        SbUnixSignalsStub unixSignalsStub;

        [SetUp]
        public void SetUp()
        {
            unixSignalsStub = new SbUnixSignalsStub();
        }

        [Test]
        public void GetShouldStopBeforeSet()
        {
            const int SIGNAL_NUMBER = 1;
            Assert.Throws<InvalidOperationException>(()
                => unixSignalsStub.GetShouldStop(SIGNAL_NUMBER),
                "Expected GetShouldStop to throw an exception when trying to get the stop value " +
                "for a signal that hasn't been set.");
        }
    }
}

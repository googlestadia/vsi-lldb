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
using YetiVSI.DebugEngine;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class LldbTransportSessionTests
    {
        [Test]
        public void MultipleSessions()
        {
            var transportSession1 = new LldbTransportSession();
            var transportSession2 = new LldbTransportSession();

            Assert.AreNotEqual(LldbTransportSession.INVALID_SESSION_ID,
                transportSession1.GetSessionId());
            Assert.AreNotEqual(LldbTransportSession.INVALID_SESSION_ID,
                transportSession2.GetSessionId());
            Assert.AreNotEqual(transportSession1.GetSessionId(), transportSession2.GetSessionId());

            // Make sure the port numbers are different for the two sessions.
            Assert.AreNotEqual(transportSession1.GetLocalDebuggerPort(),
                transportSession2.GetLocalDebuggerPort());
            Assert.AreNotEqual(transportSession1.GetReservedLocalAndRemotePort(),
                transportSession2.GetReservedLocalAndRemotePort());
        }
    }
}
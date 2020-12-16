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
using YetiVSI.LoadSymbols;
using LaunchOption = YetiVSI.DebugEngine.DebugEngine.LaunchOption;

namespace YetiVSI.Test.LoadSymbols
{
    [TestFixture]
    public class SessionNotifierServiceTests
    {
        [Test]
        public void TestEventHandlerIsInvokedWhenLaunchNotificationSent()
        {
            ISessionNotifier notifier = new SessionNotifierService();

            bool invoked = false;
            notifier.SessionLaunched += (sender, args) => invoked = true;

            notifier.NotifySessionLaunched(
                new SessionLaunchedEventArgs(LaunchOption.LaunchGame, null));

            Assert.IsTrue(invoked);
        }

        [Test]
        public void TestEventHandlerIsInvokedWhenStopNotificationSent()
        {
            ISessionNotifier notifier = new SessionNotifierService();

            bool invoked = false;
            notifier.SessionStopped += (sender, args) => invoked = true;

            notifier.NotifySessionStopped(new SessionStoppedEventArgs(null));

            Assert.IsTrue(invoked);
        }
    }
}
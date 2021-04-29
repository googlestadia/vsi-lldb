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

﻿using DebuggerApi;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using System;
using System.Threading;
using YetiVSI.DebugEngine;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class LldbListenerSubscriberTests
    {
        readonly TimeSpan _maxTimeSpanWaitingForEvent = TimeSpan.FromMilliseconds(500);
        readonly string _fileUpdateExample =
            "0000019CF1ADB980 Event: broadcaster = 0000019CE845EEE8 (lldb.stadia.broadcaster), type = 0x00000020 (file-update), data = {{\"file\":\"/usr/local/cloudcast/lib/libggp.so\", \r\n \"method\":0, \"size\":10}}";

        [Test]
        public void SubscriberWithFileUpdateAvailableTest()
        {
            var eventReceived = new AutoResetEvent(false);
            var mockSbEvent = Substitute.For<SbEvent>();
            mockSbEvent.GetDescription().Returns(_fileUpdateExample);
            mockSbEvent.GetEventType().Returns(EventType.STRUCTURED_DATA);

            var mockSbListener = Substitute.For<SbListener>();
            mockSbListener.WaitForEvent(Arg.Any<uint>(), out var _).Returns(x => {
                x[1] = mockSbEvent;
                return true;
            });

            var lldbListenerSubscriber = new LldbListenerSubscriber(mockSbListener);

            var calledStateChanged = 0;
            var calledExceptionOccured = 0;
            var calledFileUpdate = 0;
            var calledBreakpointChanged = 0;
            FileProcessingUpdate fileUpdate = null;

            lldbListenerSubscriber.StateChanged += (c, e) =>
                UpdateCounterAndSetEventReceived(ref calledStateChanged, eventReceived);
            lldbListenerSubscriber.ExceptionOccured += (c, e) =>
                UpdateCounterAndSetEventReceived(ref calledExceptionOccured, eventReceived);
            lldbListenerSubscriber.FileUpdateReceived += (c, e) =>
            {
                calledFileUpdate++;
                fileUpdate = e?.Update;
                eventReceived.Set();
            };
            lldbListenerSubscriber.BreakpointChanged += (c, e) =>
                UpdateCounterAndSetEventReceived(ref calledBreakpointChanged, eventReceived);

            lldbListenerSubscriber.Start();
            Assert.IsTrue(eventReceived.WaitOne(_maxTimeSpanWaitingForEvent));
            lldbListenerSubscriber.Stop();

            Assert.That(calledExceptionOccured, Is.EqualTo(0));
            Assert.That(calledStateChanged, Is.EqualTo(0));
            Assert.That(calledBreakpointChanged, Is.EqualTo(0));
            Assert.That(calledFileUpdate, Is.EqualTo(1));
            Assert.That(fileUpdate, Is.Not.Null);
            Assert.That(fileUpdate.File, Is.EqualTo("/usr/local/cloudcast/lib/libggp.so"));
            Assert.That(fileUpdate.Size, Is.EqualTo(10));
            Assert.That(fileUpdate.Method, Is.EqualTo(FileProcessingState.Read));
        }

        [Test]
        public void SubscriberWithStateEventAvailableTest()
        {
            var eventReceived = new AutoResetEvent(false);
            var mockSbEvent = Substitute.For<SbEvent>();
            mockSbEvent.GetEventType().Returns(EventType.STATE_CHANGED);

            var mockSbListener = Substitute.For<SbListener>();
            mockSbListener.WaitForEvent(Arg.Any<uint>(), out var _).Returns(x => {
                x[1] = mockSbEvent;
                return true;
            });

            var lldbListenerSubscriber = new LldbListenerSubscriber(mockSbListener);

            var calledStateChanged = 0;
            var calledExceptionOccured = 0;
            var calledFileUpdate = 0;
            var calledBreakpointChanged = 0;
            lldbListenerSubscriber.StateChanged += (c, e) =>
                UpdateCounterAndSetEventReceived(ref calledStateChanged, eventReceived);
            lldbListenerSubscriber.ExceptionOccured += (c, e) =>
                UpdateCounterAndSetEventReceived(ref calledExceptionOccured, eventReceived);
            lldbListenerSubscriber.FileUpdateReceived += (c, e) =>
                UpdateCounterAndSetEventReceived(ref calledFileUpdate, eventReceived);
            lldbListenerSubscriber.BreakpointChanged += (c, e) =>
                UpdateCounterAndSetEventReceived(ref calledBreakpointChanged, eventReceived);

            lldbListenerSubscriber.Start();
            Assert.IsTrue(eventReceived.WaitOne(_maxTimeSpanWaitingForEvent));
            lldbListenerSubscriber.Stop();

            Assert.That(calledExceptionOccured, Is.EqualTo(0));
            Assert.That(calledStateChanged, Is.EqualTo(1));
            Assert.That(calledFileUpdate, Is.EqualTo(0));
            Assert.That(calledBreakpointChanged, Is.EqualTo(0));
        }

        [Test]
        public void SubscriberWithStateEventInterruptedTest()
        {
            var eventReceived = new AutoResetEvent(false);
            var mockSbEvent = Substitute.For<SbEvent>();
            mockSbEvent.GetEventType().Returns(EventType.INTERRUPT);

            var mockSbListener = Substitute.For<SbListener>();
            mockSbListener.WaitForEvent(Arg.Any<uint>(), out var _).Returns(x => {
                x[1] = mockSbEvent;
                return true;
            });

            mockSbListener.WaitForEvent(Arg.Any<uint>(), out var _).Returns(x => {
                eventReceived.Set(); // stop listening for the events
                x[1] = mockSbEvent;
                return true;
            });

            var lldbListenerSubscriber = new LldbListenerSubscriber(mockSbListener);
            var calledStateChanged = 0;
            var calledExceptionOccured = 0;
            var calledFileUpdate = 0;
            var calledBreakpointChanged = 0;
            lldbListenerSubscriber.StateChanged += (c, e) =>
                UpdateCounterAndSetEventReceived(ref calledStateChanged, eventReceived);
            lldbListenerSubscriber.ExceptionOccured += (c, e) =>
                UpdateCounterAndSetEventReceived(ref calledExceptionOccured, eventReceived);
            lldbListenerSubscriber.FileUpdateReceived += (c, e) =>
                UpdateCounterAndSetEventReceived(ref calledFileUpdate, eventReceived);
            lldbListenerSubscriber.BreakpointChanged += (c, e) =>
                UpdateCounterAndSetEventReceived(ref calledBreakpointChanged, eventReceived);

            lldbListenerSubscriber.Start();
            Assert.IsTrue(eventReceived.WaitOne(_maxTimeSpanWaitingForEvent));
            lldbListenerSubscriber.Stop();

            Assert.That(calledExceptionOccured, Is.EqualTo(0));
            Assert.That(calledStateChanged, Is.EqualTo(0));
            Assert.That(calledFileUpdate, Is.EqualTo(0));
            Assert.That(calledBreakpointChanged, Is.EqualTo(0));
        }

        [Test]
        public void SubscriberWithEventRaisingExceptionTest()
        {
            var eventReceived = new AutoResetEvent(false);
            var mockSbListener = Substitute.For<SbListener>();
            mockSbListener.WaitForEvent(Arg.Any<uint>(), out var _).Throws(new Exception("oops"));

            var lldbListenerSubscriber = new LldbListenerSubscriber(mockSbListener);

            var calledStateChanged = 0;
            var calledExceptionOccured = 0;
            var calledFileUpdate = 0;
            var calledBreakpointChanged = 0;
            lldbListenerSubscriber.StateChanged += (c, e) =>
                UpdateCounterAndSetEventReceived(ref calledStateChanged, eventReceived);
            lldbListenerSubscriber.ExceptionOccured += (c, e) =>
                UpdateCounterAndSetEventReceived(ref calledExceptionOccured, eventReceived);
            lldbListenerSubscriber.FileUpdateReceived += (c, e) =>
                UpdateCounterAndSetEventReceived(ref calledFileUpdate, eventReceived);
            lldbListenerSubscriber.BreakpointChanged += (c, e) =>
                UpdateCounterAndSetEventReceived(ref calledBreakpointChanged, eventReceived);

            lldbListenerSubscriber.Start();
            Assert.IsTrue(eventReceived.WaitOne(_maxTimeSpanWaitingForEvent));
            lldbListenerSubscriber.Stop();

            Assert.That(calledExceptionOccured, Is.EqualTo(1));
            Assert.That(calledStateChanged, Is.EqualTo(0));
            Assert.That(calledFileUpdate, Is.EqualTo(0));
            Assert.That(calledBreakpointChanged, Is.EqualTo(0));
        }

        [Test]
        public void SubscriberWithBreakpointEventAvailableTest()
        {
            var eventReceived = new AutoResetEvent(false);
            var mockSbEvent = Substitute.For<SbEvent>();
            mockSbEvent.GetEventType().Returns(EventType.STATE_CHANGED);
            mockSbEvent.IsBreakpointEvent.Returns(true);

            var mockSbListener = Substitute.For<SbListener>();
            mockSbListener.WaitForEvent(Arg.Any<uint>(), out var _).Returns(x => {
                x[1] = mockSbEvent;
                return true;
            });

            var lldbListenerSubscriber = new LldbListenerSubscriber(mockSbListener);

            var calledStateChanged = 0;
            var calledExceptionOccurred = 0;
            var calledFileUpdate = 0;
            var calledBreakpointChanged = 0;
            lldbListenerSubscriber.StateChanged += (c, e) =>
                UpdateCounterAndSetEventReceived(ref calledStateChanged, eventReceived);
            lldbListenerSubscriber.ExceptionOccured += (c, e) =>
                UpdateCounterAndSetEventReceived(ref calledExceptionOccurred, eventReceived);
            lldbListenerSubscriber.FileUpdateReceived += (c, e) =>
                UpdateCounterAndSetEventReceived(ref calledFileUpdate, eventReceived);
            lldbListenerSubscriber.BreakpointChanged += (c, e) =>
                UpdateCounterAndSetEventReceived(ref calledBreakpointChanged, eventReceived);

            lldbListenerSubscriber.Start();
            Assert.IsTrue(eventReceived.WaitOne(_maxTimeSpanWaitingForEvent));
            lldbListenerSubscriber.Stop();

            Assert.That(calledExceptionOccurred, Is.EqualTo(0));
            Assert.That(calledStateChanged, Is.EqualTo(0));
            Assert.That(calledFileUpdate, Is.EqualTo(0));
            Assert.That(calledBreakpointChanged, Is.EqualTo(1));
        }

        void UpdateCounterAndSetEventReceived(ref int counter, AutoResetEvent eventAwaiter)
        {
            counter++;
            eventAwaiter.Set();
        }
    }
}

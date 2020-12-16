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

﻿using Microsoft.VisualStudio.Debugger.Interop;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using YetiCommon;
using YetiVSI.DebugEngine;
using YetiVSI.Test.TestSupport.Lldb;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class LldbExceptionManagerTests
    {
        Dictionary<int, Signal> defaultSignals = new Dictionary<int, Signal>()
        {
            {1, new Signal { name = "SIGHUP", code = 1, stop = true} },
            {2, new Signal { name = "SIGINT", code = 2, stop = true} },
            {3, new Signal { name = "SIGQUIT", code = 3, stop = false} },
        };
        RemoteTargetStub sbTargetStub;
        SbProcessStub sbProcessStub;
        SbUnixSignalsStub sbUnixSignalsStub;
        LldbExceptionManager exceptionManager;

        [SetUp]
        public void SetUp()
        {
            sbTargetStub = new RemoteTargetStub("test-target");
            sbUnixSignalsStub = new SbUnixSignalsStub();
            sbProcessStub = new SbProcessStub(sbTargetStub, sbUnixSignalsStub);
            exceptionManager = new LldbExceptionManager(sbProcessStub,
                defaultSignals);
        }

        [Test]
        public void ConstructorTest()
        {
            foreach (var signal in defaultSignals)
            {
                Assert.AreEqual(signal.Value.stop, sbUnixSignalsStub.GetShouldStop(signal.Key),
                    $"Signal {signal.Value.name} has incorrect stop state.");
            }
        }

        [Test]
        public void SetExceptionsWrongGuid()
        {
            Signal firstDefaultSignal = defaultSignals.Values.ToList()[0];
            Assert.True(firstDefaultSignal.stop, "Expected the stop value for the first default "
                + "signal to be true.");
            // Set an exception that would change the stop value for a signal
            EXCEPTION_INFO exception = new EXCEPTION_INFO
            {
                guidType = new Guid("0123456789abcdef0123456789abcedf"),
                dwCode = (uint)firstDefaultSignal.code,
                dwState = AD7Constants.VsExceptionContinueState,
            };
            exceptionManager.SetExceptions(new EXCEPTION_INFO[] { exception });
            // Since the GUID type was not one we want to handle, verify the stop value hasn't
            // changed.
            Assert.AreEqual(firstDefaultSignal.stop,
                sbUnixSignalsStub.GetShouldStop(firstDefaultSignal.code),
                $"Signal {firstDefaultSignal.name} has incorrect stop state.");
        }

        [Test]
        public void SetExceptionsWrongSignal()
        {
            const int invalidSignal = 5000;
            // Set an exception that would change the stop value for a signal
            EXCEPTION_INFO exception = new EXCEPTION_INFO
            {
                guidType = YetiConstants.DebugEngineGuid,
                dwCode = invalidSignal
            };
            exceptionManager.SetExceptions(new EXCEPTION_INFO[] { exception });
            // Since the GUID type was not one we want to handle, verify the stop value hasn't
            // changed.
            Assert.False(sbUnixSignalsStub.HasShouldStop(invalidSignal), "Expected no stop "
                + "information to be available for the invalid signal.");
        }

        [Test]
        public void SetException()
        {
            Signal firstDefaultSignal = defaultSignals.Values.ToList()[0];
            Assert.True(firstDefaultSignal.stop, "Expected the stop value for the first default "
                + "signal to be true.");
            // Set an exception that would change the stop value for a signal
            EXCEPTION_INFO exception = new EXCEPTION_INFO
            {
                guidType = YetiConstants.DebugEngineGuid,
                dwCode = (uint)firstDefaultSignal.code,
                dwState = AD7Constants.VsExceptionContinueState,
            };
            exceptionManager.SetExceptions(new EXCEPTION_INFO[] { exception });
            // Verify that the stop value has changed.
            Assert.False(sbUnixSignalsStub.GetShouldStop(firstDefaultSignal.code),
                $"Signal {firstDefaultSignal.name} has incorrect stop state.");

            // Set an exception that would change the stop value for a signal back
            exception = new EXCEPTION_INFO
            {
                guidType = YetiConstants.DebugEngineGuid,
                dwCode = (uint)firstDefaultSignal.code,
                dwState = AD7Constants.VsExceptionStopState,
            };
            exceptionManager.SetExceptions(new EXCEPTION_INFO[] { exception });
            // Verify that the stop value has changed.
            Assert.True(sbUnixSignalsStub.GetShouldStop(firstDefaultSignal.code),
                $"Signal {firstDefaultSignal.name} has incorrect stop state.");
        }
    }
}

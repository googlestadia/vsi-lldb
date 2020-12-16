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

using Microsoft.VisualStudio.Debugger.Interop;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using YetiCommon;
using YetiVSI.Attributes;
using YetiVSI.DebugEngine;
using YetiVSI.Test.TestSupport;

namespace YetiVSI.Test.DebugEngine.Attributes
{
    [TestFixture]
    class ProvideStadiaExceptionAttributeTest
    {
        readonly string ENGINE_EXCEPTION_KEY =
            $"AD7Metrics\\Exception\\{{{YetiConstants.DebugEngineGuid}}}";
        const string STADIA_EXCEPTIONS_KEY = "Stadia Exceptions";
        const string LINUX_SIGNALS_KEY = "Linux Signals";
        const string CODE_KEY_VALUE = "Code";
        const string STATE_KEY_VALUE = "State";

        RegistrationContextStub registrationContextStub;
        ProvideStadiaExceptionsAttribute exceptionAttribute;
        Dictionary<string, Signal> signals = new Dictionary<string, Signal>
        {
            {"01 SIGHUP", new Signal { name = "SIGHUP", code = 1}},
            {"02 SIGINT", new Signal { name = "SIGINT", code = 2, stop = true}},
            {"11 SIGABRT / SIGIOT",  new Signal { name = "SIGABRT", code = 11, stop = true,
                alias = new List<string> { "SIGIOT" }}},
        };

        [SetUp]
        public void Setup()
        {
            registrationContextStub = new RegistrationContextStub();
            exceptionAttribute = new ProvideStadiaExceptionsAttribute(signals.Values.ToList());
        }

        [Test]
        public void Register()
        {
            exceptionAttribute.Register(registrationContextStub);
            KeyStub engineKey;
            Assert.True(registrationContextStub.keys.TryGetValue(ENGINE_EXCEPTION_KEY,
                out engineKey), MissingRegistryKeyErrorMessage(ENGINE_EXCEPTION_KEY));
            KeyStub exceptionKey;
            Assert.True(engineKey.subKeys.TryGetValue(STADIA_EXCEPTIONS_KEY,
                out exceptionKey), MissingRegistryKeyErrorMessage(STADIA_EXCEPTIONS_KEY));
            KeyStub linuxSignalKey;
            Assert.True(exceptionKey.subKeys.TryGetValue(LINUX_SIGNALS_KEY, out linuxSignalKey),
                MissingRegistryKeyErrorMessage(LINUX_SIGNALS_KEY));
            Assert.AreEqual(signals.Count, linuxSignalKey.subKeys.Count,
                "Unexpected number of signals registered.");
            foreach(var signal in signals)
            {
                KeyStub signalKey;
                Assert.True(linuxSignalKey.subKeys.TryGetValue(signal.Key, out signalKey),
                    MissingRegistryKeyErrorMessage(signal.Key));
                object signalCode;
                Assert.True(signalKey.values.TryGetValue(CODE_KEY_VALUE, out signalCode),
                    MissingRegistryKeyErrorMessage($"{signal.Key}\\{CODE_KEY_VALUE}"));
                Assert.AreEqual(signal.Value.code, signalCode, "Code registered for signal "
                    + $"{signal.Key} was different than expected.");
                object signalState;
                Assert.True(signalKey.values.TryGetValue(STATE_KEY_VALUE, out signalState),
                    MissingRegistryKeyErrorMessage($"{signal.Key}\\{STATE_KEY_VALUE}"));
                Assert.AreEqual(signal.Value.stop, ((enum_EXCEPTION_STATE)signalState).HasFlag(
                    AD7Constants.ExceptionStopState),
                    $"Stop state registered for signal {signal.Key} was different than expected.");
            }
        }

        [Test]
        public void Unregister()
        {
            exceptionAttribute.Register(registrationContextStub);
            exceptionAttribute.Unregister(registrationContextStub);
            Assert.AreEqual(0, registrationContextStub.keys.Count,
                "After unregistering there should be no registered keys.");
        }

        private string MissingRegistryKeyErrorMessage(string keyName) =>
            $"Registry key {keyName} doesn't exist.";
    }
}

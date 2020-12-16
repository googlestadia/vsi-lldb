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
using YetiCommon;
using YetiVSI.DebugEngine;
using YetiVSI.Test.TestSupport.DebugEngine;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class CommandDebugPropertyTests
    {
        private CommandDebugProperty Create(Func<string> command)
        {
            var factory = new CommandDebugProperty.Factory();
            return (CommandDebugProperty)factory.Create("dummyName", "dummyType", command);
        }

        [Test]
        public void CommandInvokedOnContinue()
        {
            bool commandInvoked = false;
            var debugProperty = Create(() =>
            {
                commandInvoked = true;
                return "CommandResult";
            });

            DEBUG_PROPERTY_INFO propertyInfo;
            debugProperty.GetPropertyInfo(
                (enum_DEBUGPROP_INFO_FLAGS)
                    enum_DEBUGPROP_INFO_FLAGS100.DEBUGPROP100_INFO_NOSIDEEFFECTS,
                out propertyInfo);

            Assert.That(commandInvoked, Is.False);
        }

        [Test]
        public void CommandInvoked()
        {
            bool commandInvoked = false;
            var debugProperty = Create(() =>
            {
                commandInvoked = true;
                return "CommandResult";
            });

            DEBUG_PROPERTY_INFO propertyInfo;
            debugProperty.GetPropertyInfo(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE,
                out propertyInfo);

            Assert.That(commandInvoked, Is.True);
            Assert.That(propertyInfo.bstrValue, Is.EqualTo("CommandResult"));
        }
    }
}

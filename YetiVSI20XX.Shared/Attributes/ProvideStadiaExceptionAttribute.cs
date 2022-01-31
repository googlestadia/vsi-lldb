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

using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using YetiCommon;
using YetiVSI.DebugEngine;

namespace YetiVSI.Attributes
{
    /// <summary>
    /// Register Stadia exceptions. These will show up in the 'Exception Settings' window in Visual
    /// Studio.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ProvideStadiaExceptionsAttribute : RegistrationAttribute
    {
        const string CODE_KEY_VALUE = "Code";
        const string STATE_KEY_VALUE = "State";

        readonly string ENGINE_EXCEPTION_KEY = $"AD7Metrics\\Exception\\" +
                                               $"{{{YetiConstants.DebugEngineGuid}}}";

        // These strings are user facing.
        const string STADIA_EXCEPTIONS = "Stadia Exceptions";
        const string LINUX_SIGNALS = "Linux Signals";

        readonly IEnumerable<Signal> signals;

        /// <summary>
        /// This constructor only exists because 'List<Signal>' is not a valid attribute parameter
        /// type, so we use a constructor that takes no parameters and has the default value baked
        /// in.
        /// </summary>
        public ProvideStadiaExceptionsAttribute() : this(LinuxSignals.GetDefaultSignalsMap().Values)
        {
        }

        public ProvideStadiaExceptionsAttribute(IEnumerable<Signal> signals)
        {
            this.signals = new List<Signal>(signals);
        }

        public override void Register(RegistrationContext context)
        {
            var engineKey = context.CreateKey(ENGINE_EXCEPTION_KEY);
            var stadiaExceptionGroup = CreateExceptionSubKey(engineKey, STADIA_EXCEPTIONS);
            var signalsExceptionGroup =
                CreateExceptionSubKey(stadiaExceptionGroup, LINUX_SIGNALS);
            foreach (var signal in signals)
            {
                CreateSignalSubKey(signalsExceptionGroup, signal);
            }
        }

        public override void Unregister(RegistrationContext context)
        {
            context.RemoveKey(ENGINE_EXCEPTION_KEY);
        }

        private Key CreateExceptionSubKey(Key parentKey, string name)
        {
            var subKey = parentKey.CreateSubkey(name);
            subKey.SetValue(CODE_KEY_VALUE, 0);
            subKey.SetValue(STATE_KEY_VALUE, Convert.ToInt32(AD7Constants.VsExceptionStopState));
            return subKey;
        }

        private Key CreateSignalSubKey(Key parentKey, Signal signal)
        {
            var displayName = CreateDisplayName(signal);
            var signalKey = parentKey.CreateSubkey(displayName);
            signalKey.SetValue(CODE_KEY_VALUE, signal.code);
            signalKey.SetValue(STATE_KEY_VALUE,
                signal.stop
                    ? Convert.ToInt32(AD7Constants.VsExceptionStopState)
                    : Convert.ToInt32(AD7Constants.VsExceptionContinueState));
            return signalKey;
        }

        private string CreateDisplayName(Signal signal)
        {
            var displayName = $"{signal.code:D2} {signal.name}";

            if (signal.alias == null)
            {
                return displayName;
            }

            return string.Join(" / ", Enumerable.Repeat(displayName, 1).Concat(signal.alias));
        }
    }
}
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

using System;
using DebuggerApi;
using System.Collections.Generic;

namespace YetiVSI.Test.TestSupport.Lldb
{
    public class SbUnixSignalsStub : SbUnixSignals
    {
        readonly Dictionary<int, bool> signals = new Dictionary<int, bool>();

        public bool HasShouldStop(int signalNumber)
        {
            return signals.ContainsKey(signalNumber);
        }

        public bool GetShouldStop(int signalNumber)
        {
            bool value;
            if (signals.TryGetValue(signalNumber, out value))
            {
                return value;
            }
            // Instead of making assumptions about the behaviour when trying to get the stop value
            // before it's been set, throw an exception.
            // The LldbExceptionManager should set the default value for all signals when it's
            // created. If we try to get the stop value of a signal that wasn't set something went
            // wrong.
            throw new InvalidOperationException("Trying to get the stop value for a signal that" +
                "wasn't set.");
        }

        public bool SetShouldStop(int signalNumber, bool value)
        {
            signals[signalNumber] = value;
            return true;
        }
    }
}

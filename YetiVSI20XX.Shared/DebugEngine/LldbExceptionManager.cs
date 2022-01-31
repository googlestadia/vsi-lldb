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

using DebuggerApi;
using Microsoft.VisualStudio.Debugger.Interop;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using YetiCommon;
using YetiVSI.DebugEngine.Interfaces;

namespace YetiVSI.DebugEngine
{
    /// <summary>
    /// Responsible for how exceptions are handled.
    /// </summary>
    public class LldbExceptionManager : IExceptionManager
    {
        public class Factory
        {
            IReadOnlyDictionary<int, Signal> defaultSignals;

            public Factory(IReadOnlyDictionary<int, Signal> defaultSignals)
            {
                this.defaultSignals = defaultSignals;
            }

            public virtual IExceptionManager Create(SbProcess lldbProcess)
            {
                return new LldbExceptionManager(lldbProcess, defaultSignals);
            }
        }

        readonly SbProcess lldbProcess;
        IReadOnlyDictionary<int, Signal> defaultSignals;

        public LldbExceptionManager(SbProcess lldbProcess, IReadOnlyDictionary<int, Signal> defaultSignals)
        {
            this.lldbProcess = lldbProcess;
            this.defaultSignals = defaultSignals;
            ResetToDefaults();
        }

        public void SetExceptions(IEnumerable<EXCEPTION_INFO> exceptions)
        {
            var linuxSignals = exceptions.Where(IsStadiaException).Where(IsSignal).Select(
                ConvertToSignal);
            SetSignals(linuxSignals);
        }

        private void ResetToDefaults()
        {
            SetSignals(defaultSignals.Values.ToList());
        }

        private void SetSignals(IEnumerable<Signal> signals)
        {
            var unixSignals = lldbProcess.GetUnixSignals();
            foreach (var signal in signals)
            {
                Trace.WriteLine($"Setting {signal.name} to stop: {signal.stop}");
                unixSignals.SetShouldStop(signal.code, signal.stop);
            }
        }

        /// <summary>
        /// Convert EXCEPTION_INFO into Signals matching the exception code to the signal number,
        /// dropping any exceptions that can't be mapped to a signal.
        /// </summary>
        /// <exception cref="KeyNotFoundException">No signal matches the exception</exception>
        private Signal ConvertToSignal(EXCEPTION_INFO exception)
        {
            Signal signal = defaultSignals[(int)exception.dwCode];
            signal.stop =
                exception.dwState.HasFlag(AD7Constants.ExceptionStopState);
            return signal;
        }

        /// <summary>
        /// Return true for an exception that represents a signal, false otherwise.
        /// </summary>
        /// <param name="exception">Exception to inspect.</param>
        private bool IsSignal(EXCEPTION_INFO exception)
        {
            return defaultSignals.ContainsKey((int)exception.dwCode);
        }

        /// <summary>
        /// Return true for an exception that came from our debug engine, false otherwise.
        /// </summary>
        /// <param name="exception">Exception to inspect.</param>
        private bool IsStadiaException(EXCEPTION_INFO exception)
        {
            return exception.guidType == YetiConstants.DebugEngineGuid ||
                exception.guidType == YetiConstants.ExceptionEventGuid;
        }
    }
}

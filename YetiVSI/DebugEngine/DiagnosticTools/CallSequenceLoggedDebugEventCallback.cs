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
using Microsoft.VisualStudio.Threading;
using NLog;
using System;
using YetiVSI.Util;

namespace YetiVSI.DebugEngine.DiagnosticTools
{
    /// <summary>
    /// Captures call sequence digram logs for calls in and out of an IDebugEventCallback2.
    /// </summary>
    public class CallSequenceLoggedDebugEventCallback : IDebugEventCallback2
    {
        readonly JoinableTaskContext _taskContext;
        readonly IDebugEventCallback2 _callback;
        readonly ILogger _logger;

        public CallSequenceLoggedDebugEventCallback(JoinableTaskContext taskContext,
            IDebugEventCallback2 callback,
            ILogger logger)
        {
            _taskContext = taskContext;
            _callback = callback;
            _logger = logger;
        }

#region IDebugEventCallback2

        public int Event(IDebugEngine2 pEngine, IDebugProcess2 pProcess, IDebugProgram2 pProgram,
            IDebugThread2 pThread, IDebugEvent2 pEvent, ref Guid riidEvent, uint dwAttrib)
        {
            _taskContext.ThrowIfNotOnMainThread();

            _logger.Trace($"Note right of VS: <IN> IDebugEventCallback2 {pEvent.GetType()}");

            var result = _callback.Event(pEngine, pProcess, pProgram, pThread, pEvent,
                ref riidEvent, dwAttrib);

            _logger.Trace($"Note right of VS: <OUT> IDebugEventCallback2 {pEvent.GetType()}");

            return result;
        }

#endregion
    }
}
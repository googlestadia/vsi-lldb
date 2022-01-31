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

﻿using System;
using System.Threading;
using YetiCommon.PerformanceTracing;

namespace YetiVSI.DebugEngine
{
    // Since TaskExecutor is only allowed to execute one task at a time, this helper is based
    // on the assumption that this would be consistent for each event.
    public class TaskExecutorTracingHelper
    {
        readonly ITracingLogger _tracingLogger;
        readonly ITimeSource _timeSource;

        long _startTimestampTicks;

        public TaskExecutorTracingHelper(ITracingLogger tracingLogger, ITimeSource timeSource)
        {
            _tracingLogger = tracingLogger;
            _timeSource = timeSource;
        }

        public void OnAsyncTaskStarted(object sender, EventArgs args)
        {
            if (_startTimestampTicks != 0)
            {
                throw new InvalidOperationException("OnAsyncTaskStarted was already invoked");
            }
            _startTimestampTicks = _timeSource.GetTimestampTicks();
        }

        public void OnAsyncTaskEnded(object sender, AsyncTaskEndedEventArgs args)
        {
            if (_startTimestampTicks == 0)
            {
                throw new InvalidOperationException("OnAsyncEventStarted was not invoked");
            }

            long endTimestampTicks = _timeSource.GetTimestampTicks();
            long startTimestampUs = _timeSource.ConvertTicksToUs(_startTimestampTicks);
            long durationUs = _timeSource.GetDurationUs(_startTimestampTicks, endTimestampTicks);
            int threadId = Thread.CurrentThread.ManagedThreadId;

            _tracingLogger.TraceEvent(args.CallerName, EventType.Async, args.CallerType, durationUs,
                                      startTimestampUs, threadId);
            _startTimestampTicks = 0;
        }
    }
}

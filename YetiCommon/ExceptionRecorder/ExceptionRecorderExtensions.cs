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
using System.Diagnostics;
using System.Reflection;

namespace YetiCommon.ExceptionRecorder
{
    public static class ExceptionRecorderExtensions
    {
        /// <summary>
        /// Records an exception using the caller of this method as the callsite.
        /// </summary>
        /// <param name="ex">Exception to be recorded. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown if ex is null</exception>
        public static void RecordHere(this IExceptionRecorder recorder, Exception ex)
        {
            // Create stack trace that skips the current frame.
            var stackTrace = new StackTrace(1);
            Record(recorder, ex, stackTrace);
        }

        /// <summary>
        /// Records an exception using the caller of this method as the callsite.
        /// Does not throw exceptions.
        /// </summary>
        public static void SafelyRecordHere(this IExceptionRecorder recorder, Exception ex)
        {
            StackTrace stackTrace;
            try
            {
                // Create stack trace that skips the current frame.
                stackTrace = new StackTrace(1);
            }
            catch (Exception e)
            {
                Trace.WriteLine("Unable to capture stack trace: " + e.ToString());
                return;
            }

            SafeErrorUtil.SafelyLogError(() => Record(recorder, ex, stackTrace),
                "Recording exception");
        }

        static void Record(IExceptionRecorder recorder, Exception ex, StackTrace stackTrace)
        {
            if (ex == null)
            {
                throw new ArgumentNullException(nameof(ex));
            }
            if (stackTrace.FrameCount > 0)
            {
                var callerInfo = stackTrace.GetFrame(0).GetMethod();
                recorder.Record(callerInfo, ex);
            }
            else
            {
                RecordNoCaller(recorder, ex);
            }
        }

        static void RecordNoCaller(IExceptionRecorder recorder, Exception ex)
        {
            // Not sure why we have no caller. Use fake caller info.
            recorder.Record(MethodBase.GetCurrentMethod(), ex);
        }
    }
}

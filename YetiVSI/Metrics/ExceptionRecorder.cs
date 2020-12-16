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
using System.Reflection;
using YetiCommon.ExceptionRecorder;
using YetiVSI.Shared.Metrics;

namespace YetiVSI.Metrics
{
    /// <summary>
    /// Implement <see cref="IExceptionRecorder"/> by recording a metrics event.
    /// </summary>
    public class ExceptionRecorder : IExceptionRecorder
    {
        const uint DefaultMaxExceptionsChainLength = 10;

        IMetrics metrics;
        uint maxExceptionsChainLength;

        /// <summary>
        /// Create an ExceptionRecorder
        /// </summary>
        /// <param name="metrics">Metrics service to record events.</param>
        /// <param name="maxExceptionsChainLength">Maximum number of exception to record.</param>
        /// <remarks>
        /// If there are more exceptions than maxExceptionsChainLength, records a
        /// <see cref="ChainTooLongException"/> after the last exception recorded.
        /// Effectively we record maxExceptionsChainLength+1 exceptions in total.
        /// </remarks>
        public ExceptionRecorder(IMetrics metrics,
            uint maxExceptionsChainLength = DefaultMaxExceptionsChainLength)
        {
            if (metrics == null)
            {
                throw new ArgumentNullException(nameof(metrics));
            }
            this.metrics = metrics;
            this.maxExceptionsChainLength = maxExceptionsChainLength;
        }

        #region IExceptionRecorder

        public void Record(MethodBase callSite, Exception ex)
        {
            if (callSite == null)
            {
                throw new ArgumentNullException(nameof(callSite));
            }
            if (ex == null)
            {
                throw new ArgumentNullException(nameof(ex));
            }

            var data = new VSIExceptionData();
            data.CatchSite = callSite.GetProto();
            RecordExceptionChain(ex, data);
            var logEvent = new DeveloperLogEvent();
            logEvent.ExceptionsData.Add(data);
            logEvent.MergeFrom(ExceptionHelper.RecordException(ex));
            metrics.RecordEvent(DeveloperEventType.Types.Type.VsiException, logEvent);
        }

        #endregion

        void RecordExceptionChain(Exception ex, VSIExceptionData data)
        {
            for (uint i = 0; i < maxExceptionsChainLength && ex != null; i++)
            {
                var exData = new VSIExceptionData.Types.Exception();
                exData.ExceptionType = ex.GetType().GetProto();
                // TODO: record the exception stack trace.
                data.ExceptionsChain.Add(exData);
                ex = ex.InnerException;
            }
            if (ex != null)
            {
                data.ExceptionsChain.Add(
                    new VSIExceptionData.Types.Exception
                    {
                        ExceptionType = typeof(ChainTooLongException).GetProto()
                    });
            }
        }

        public class ChainTooLongException : Exception { }
    }
}

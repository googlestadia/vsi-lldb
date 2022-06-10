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
using System.Reflection;
using Metrics.Shared;
using YetiCommon.ExceptionRecorder;

namespace YetiVSI.Metrics
{
    /// <summary>
    /// Implement <see cref="IExceptionRecorder"/> by recording a metrics event.
    /// </summary>
    public class ExceptionRecorder : IExceptionRecorder
    {
        const int _defaultMaxExceptionsChainLength = 10;
        const int _defaultMaxStackTraceFrames = 100;

        static readonly string[] _namespaceAllowList =
        {
            "YetiVSI", "YetiCommon", "Castle", "ChromeClientLauncher", "CloudGrpc",
            "DebuggerApi", "DebuggerCommonApi", "DebuggerGrpc", "DebuggerGrpcClient",
            "DebuggerGrpcServer", "GgpDumpExtension", "GgpGrpc", "Google", "LldbApi",
            "ProcessManagerCommon", "SymbolStores", "System"
        };

        readonly IVsiMetrics _metrics;
        readonly IExceptionWriter _writer;

        /// <summary>
        /// Create an ExceptionRecorder
        /// </summary>
        /// <param name="metrics">Metrics service to record events.</param>
        /// <param name="writer">Exception writer, which writes
        /// exception data to <see cref="VSIExceptionData"/>.</param>
        /// <param name="maxExceptionsChainLength">Maximum number of exceptions to record.</param>
        /// <param name="maxStackTraceFrames">Maximum number of stack trace frames to record per
        /// exception.</param>
        /// <remarks>
        /// If there are more exceptions than maxExceptionsChainLength, records a
        /// <see cref="ExceptionWriter.ChainTooLongException"/> after the last exception recorded.
        /// Effectively we record maxExceptionsChainLength+1 exceptions in total.
        /// </remarks>
        public ExceptionRecorder(IVsiMetrics metrics, IExceptionWriter writer = null,
                                 int maxExceptionsChainLength = _defaultMaxExceptionsChainLength,
                                 int maxStackTraceFrames = _defaultMaxStackTraceFrames)
        {
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            if (writer == null)
            {
                writer = new ExceptionWriter(_namespaceAllowList, maxExceptionsChainLength,
                                             maxStackTraceFrames);
            }

            _writer = writer;
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
            _writer.WriteToExceptionData(callSite, ex, data);
            var logEvent = new DeveloperLogEvent();
            logEvent.ExceptionsData.Add(data);
            logEvent.MergeFrom(ExceptionHelper.RecordException(ex));
            _metrics.RecordEvent(DeveloperEventType.Types.Type.VsiException, logEvent);
        }

        #endregion
    }
}
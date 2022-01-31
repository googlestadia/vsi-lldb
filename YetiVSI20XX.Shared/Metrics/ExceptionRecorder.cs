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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

        static readonly string[] _namespaceAllowlist =
        {
            "YetiVSI", "YetiCommon", "Castle", "ChromeClientLauncher", "CloudGrpc",
            "DebuggerApi", "DebuggerCommonApi", "DebuggerGrpc", "DebuggerGrpcClient",
            "DebuggerGrpcServer", "GgpDumpExtension", "GgpGrpc", "Google", "LldbApi",
            "ProcessManagerCommon", "SymbolStores", "System"
        };

        readonly IVsiMetrics _metrics;
        readonly int _maxExceptionsChainLength;
        readonly int _maxStackTraceFrames;

        /// <summary>
        /// Create an ExceptionRecorder
        /// </summary>
        /// <param name="metrics">Metrics service to record events.</param>
        /// <param name="maxExceptionsChainLength">Maximum number of exceptions to record.</param>
        /// <param name="maxStackTraceFrames">Maximum number of stack trace frames to record per
        /// exception.</param>
        /// <remarks>
        /// If there are more exceptions than maxExceptionsChainLength, records a
        /// <see cref="ChainTooLongException"/> after the last exception recorded.
        /// Effectively we record maxExceptionsChainLength+1 exceptions in total.
        /// </remarks>
        public ExceptionRecorder(IVsiMetrics metrics,
                                 int maxExceptionsChainLength = _defaultMaxExceptionsChainLength,
                                 int maxStackTraceFrames = _defaultMaxStackTraceFrames)
        {
            if (metrics == null)
            {
                throw new ArgumentNullException(nameof(metrics));
            }

            _metrics = metrics;
            _maxExceptionsChainLength = maxExceptionsChainLength;
            _maxStackTraceFrames = maxStackTraceFrames;
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
            _metrics.RecordEvent(DeveloperEventType.Types.Type.VsiException, logEvent);
        }

        #endregion

        void RecordExceptionChain(Exception ex, VSIExceptionData data)
        {
            for (uint i = 0; i < _maxExceptionsChainLength && ex != null; i++)
            {
                var exData = new VSIExceptionData.Types.Exception();
                exData.ExceptionType = ex.GetType().GetProto();
                exData.ExceptionStackTraceFrames.AddRange(GetStackTraceFrames(ex));

                // TODO: record the exception stack trace.
                data.ExceptionsChain.Add(exData);
                ex = ex.InnerException;
            }

            if (ex != null)
            {
                data.ExceptionsChain.Add(new VSIExceptionData.Types.Exception
                {
                    ExceptionType = typeof(ChainTooLongException)
                        .GetProto()
                });
            }
        }

        List<VSIExceptionData.Types.Exception.Types.StackTraceFrame> GetStackTraceFrames(
            Exception ex)
        {
            var frames = new List<VSIExceptionData.Types.Exception.Types.StackTraceFrame>();
            var stackTrace = new StackTrace(ex, true);

            for (int curIndex = 0;
                curIndex < stackTrace.GetFrames()?.Length && curIndex < _maxStackTraceFrames;
                curIndex++)
            {
                var curFrame = stackTrace.GetFrame(curIndex);
                var curTransformedFrame =
                    new VSIExceptionData.Types.Exception.Types.StackTraceFrame();

                curTransformedFrame.AllowedNamespace =
                    IsMethodInAllowedNamespace(curFrame.GetMethod());

                if (curTransformedFrame.AllowedNamespace.Value)
                {
                    curTransformedFrame.Method = curFrame.GetMethod().GetProto();
                    curTransformedFrame.Filename = Path.GetFileName(curFrame.GetFileName());
                    curTransformedFrame.LineNumber = (uint?) curFrame.GetFileLineNumber();
                }

                frames.Add(curTransformedFrame);
            }

            return frames;
        }

        bool IsMethodInAllowedNamespace(MethodBase method)
        {
            string methodNamespace = method?.DeclaringType?.Namespace;

            if (string.IsNullOrEmpty(methodNamespace))
            {
                return false;
            }

            foreach (string curAllowedNamespace in _namespaceAllowlist)
            {
                if (methodNamespace.StartsWith(curAllowedNamespace + ".") ||
                    methodNamespace == curAllowedNamespace)
                {
                    return true;
                }
            }

            return false;
        }

        public class ChainTooLongException : Exception
        {
        }
    }
}
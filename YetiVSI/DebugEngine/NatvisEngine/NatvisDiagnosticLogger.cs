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

﻿using NLog;
using System;
using System.Diagnostics;
using YetiCommon.CastleAspects;

namespace YetiVSI.DebugEngine.NatvisEngine
{
    /// <summary>
    ///  The logging level denotes the amount of information to display in the logs.
    ///
    ///  All levels up to and including the selected logging level are logged. This means that
    ///  if the selected logging level is verbose, everything is logged. As such <strong>the
    ///  ordering of these values matters</strong>.
    /// </summary>
    public enum NatvisLoggingLevel
    {
        OFF,
        ERROR,
        WARNING,
        VERBOSE,
    }

    public class NatvisDiagnosticLogger : SimpleDecoratorSelf<NatvisDiagnosticLogger>
    {
        public event EventHandler<NatvisLogEventArgs> NatvisLogEvent;

        public class NatvisLogEventArgs
        {
            public NatvisLogEventArgs(LogLevel level, string message)
            {
                Level = level;
                Message = message;
            }

            public LogLevel Level { get; private set; }

            public string Message { get; private set; }
        }

        private NatvisLoggingLevel level;

        private ILogger logger;

        /// <param name="logger">
        ///  The internal NLog logger to use to output messages.
        ///
        ///  This should not be null, if no logging is desired, pass in a null logger, by calling
        ///  <c>NLog.LogFactory.CreateNullLogger()</c>, or set the logging level to
        ///  <c>OFF</c>.
        /// </param>
        /// <param name="level">
        ///  The desired verbosity of logging.
        ///  All levels up to and including the selected logging level are logged.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        ///  Thrown when <paramref name="logger"/> is <c>null</c>.
        /// </exception>
        public NatvisDiagnosticLogger(ILogger logger, NatvisLoggingLevel level)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            this.logger = logger;
            SetLogLevel(level);
        }

        /// <summary>
        /// Specify the max log level that should be captured.
        /// </summary>
        /// <param name="level">
        /// The desired verbosity of logging.
        /// All levels up to and including the selected logging level are logged.
        /// </param>
        public void SetLogLevel(NatvisLoggingLevel level)
        {
            this.level = level;
            Trace.WriteLine($"Natvis log level set to {level}.");
        }

        /// <summary>
        /// Returns true if the specified log level should be captured.
        /// </summary>
        public bool ShouldLog(NatvisLoggingLevel level)
        {
            return this.level >= level;
        }

        /// <param name="level">The severity of this message.</param>
        /// <param name="message">
        ///  A lambda returning the message string, which will only be called if the message is
        ///  actually going to be logged.
        /// </param>
        /// <exception cref="System.ComponentModel.InvalidEnumArgumentException">
        ///  Thrown if the <paramref name="level"/> is not a valid <c>LoggingLevel</c>.
        /// </exception>
        public void Log(NatvisLoggingLevel level, string message)
        {
            switch (level)
            {
                case NatvisLoggingLevel.OFF:     return;
                case NatvisLoggingLevel.ERROR:   Error(message);   return;
                case NatvisLoggingLevel.WARNING: Warning(message); return;
                case NatvisLoggingLevel.VERBOSE: Verbose(message); return;
                default: throw new System.ComponentModel.InvalidEnumArgumentException(
                    $"Unhandled logging level [{level}] for message [{message}]");
            }
        }

        /// <param name="level">The severity of this message.</param>
        /// <param name="deferredMessage">
        ///  A lambda returning the message string, which will only be called if the message is
        ///  actually going to be logged.
        /// </param>
        /// <exception cref="System.ComponentModel.InvalidEnumArgumentException">
        ///  Thrown if the <paramref name="level"/> is not a valid <c>LoggingLevel</c>.
        /// </exception>
        public void Log(NatvisLoggingLevel level, LogMessageGenerator deferredMessage)
        {
            switch (level)
            {
                case NatvisLoggingLevel.OFF:     return;
                case NatvisLoggingLevel.ERROR:   Error(deferredMessage);   return;
                case NatvisLoggingLevel.WARNING: Warning(deferredMessage); return;
                case NatvisLoggingLevel.VERBOSE: Verbose(deferredMessage); return;
                default: throw new System.ComponentModel.InvalidEnumArgumentException(
                    $"Unhandled logging level [{level}] for message [{deferredMessage()}]");
            }
        }

        public void Error(string message)
        {
            if (ShouldLog(NatvisLoggingLevel.ERROR))
            {
                Write(LogLevel.Error, $"ERROR: {message}");
            }
        }

        public void Error(LogMessageGenerator deferredMessage)
        {
            if (ShouldLog(NatvisLoggingLevel.ERROR))
            {
                Write(LogLevel.Error, $"ERROR: {deferredMessage()}");
            }
        }
        public void Warning(string message)
        {
            if (ShouldLog(NatvisLoggingLevel.WARNING))
            {
                Write(LogLevel.Warn, $"WARNING: {message}");
            }
        }

        public void Warning(LogMessageGenerator deferredMessage)
        {
            if (ShouldLog(NatvisLoggingLevel.WARNING))
            {
                Write(LogLevel.Warn, $"WARNING: {deferredMessage()}");
            }
        }

        public void Verbose(string message)
        {
            if (ShouldLog(NatvisLoggingLevel.VERBOSE))
            {
                Write(LogLevel.Info, $"INFO: {message}");
            }
        }

        public void Verbose(LogMessageGenerator deferredMessage)
        {
            if (ShouldLog(NatvisLoggingLevel.VERBOSE))
            {
                Write(LogLevel.Info, $"INFO: {deferredMessage()}");
            }
        }

        // This function is used to wrap the NLog ILogger, while still allowing us to get the
        // benefit of callsite telling us the right spot.
        // https://stackoverflow.com/questions/5132759/nlog-callsite-is-wrong-when-wrapper-is-used/5136555#answer-5136555
        private void Write(LogLevel level, string format, params object[] args)
        {
            var logEventInfo = new LogEventInfo(level, logger.Name, null, format, args);
            logger.Log(typeof(NatvisDiagnosticLogger), logEventInfo);

            NatvisLogEvent?.Invoke(Self,
                new NatvisLogEventArgs(level, logEventInfo.FormattedMessage));
        }
    }
}

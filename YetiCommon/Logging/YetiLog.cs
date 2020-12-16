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

using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using System;
using System.Diagnostics;

namespace YetiCommon.Logging
{
    /// <summary>
    ///  Manages the global NLog configuration.
    /// </summary>
    public class YetiLog
    {
        // We need to use the Component Logging pattern because we are running alongside other
        // Visual Studio extensions (aka components) that may also be using NLog.
        // https://github.com/NLog/NLog/wiki/Configure-component-logging
        private static LogFactory logFactory = null;

        static NLogTraceListener nlogTracelistener;

        const string GeneralFileTargetName = "file-general";
        const string TraceFileTargetName = "file-trace";
        const string TraceLoggerPrefix = "Trace";
        const string CallSequenceTargetName = "file-call-sequence";
        const string CallSequenceLoggerSuffix = "CallSequenceDiagram";

        /// <summary>
        /// Performs one-time setup for NLog configuration.
        /// This configuration is shared by all VSI components.
        /// </summary>
        /// <param name="appName">Log file name prefix</param>
        /// <param name="logDateTime">Timestamp used as log file name suffix</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when this has already been initialized.
        /// </exception>
        public static void Initialize(string appName, DateTime logDateTime)
        {
            if (string.IsNullOrEmpty(appName))
            {
                throw new ArgumentException("null or empty", nameof(appName));
            }
            if (appName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException("contains invalid file name chars", nameof(appName));
            }
            if (logFactory != null)
            {
                throw new InvalidOperationException(
                    nameof(YetiLog) + " has already been initialized.");
            }

            // Setup NLog configuration.
            var config = new LoggingConfiguration();
            string time = ToLogFileDateTime(DateTime.Now);
            SetupGeneralLogging(appName, time, ref config);
            SetupCallSequenceLogging(appName, time, ref config);
            SetupTraceLogging(appName, time, ref config);

            logFactory = new LogFactory(config);

            // Send System.Diagnotics.Debug and System.Diagnostics.Trace to NLog.
            nlogTracelistener = new NLogTraceListener();
            nlogTracelistener.LogFactory = logFactory;
            nlogTracelistener.DisableFlush = true;
            Trace.Listeners.Add(nlogTracelistener);
        }

        /// <summary>
        /// Unintialize the underlying NLog LogFactory forcing data to be flushed.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when this hasn't been initialized.
        /// </exception>
        public static void Uninitialize()
        {
            if (logFactory == null)
            {
                throw new InvalidOperationException(
                    nameof(YetiLog) + " is not in an initialized state.");
            }
            // Make sure that the configuration is properly closed when the process terminates or
            // you may lose some log output
            // Reference: https://github.com/NLog/NLog/wiki/Configure-component-logging
            logFactory.Configuration = null;
            logFactory = null;

            Trace.Listeners.Remove(nlogTracelistener);
        }

        /// <summary>
        /// For general logging, create a file target with a fixed basename, and enable log
        /// rotation for this target. Accepts events from all loggers at Debug level and higher.
        /// </summary>
        /// <param name="appName">log file prefix unique to this executable</param>
        /// <param name="time">time string to differentiate instances</param>
        private static void SetupGeneralLogging(string appName, string time,
            ref LoggingConfiguration config)
        {
            var fileTarget = CreateBaseFileTarget(appName, time);
            fileTarget.Layout = "${time} ${logger:whenEmpty=${callSite}} ${message}";
            fileTarget.ArchiveNumbering = ArchiveNumberingMode.Sequence;
            fileTarget.ArchiveAboveSize = 10 * 1024 * 1024;
            fileTarget.ArchiveEvery = FileArchivePeriod.Day;
            config.AddTarget(GeneralFileTargetName, fileTarget);
            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, fileTarget));
        }

        /// <summary>
        /// For call sequence logging, create a file target with a fixed basename, and enable log
        /// rotation for this target. Accepts events from loggers at Trace level.
        /// </summary>
        /// <param name="appName">log file prefix unique to this executable</param>
        /// <param name="time">time string to differentiate instances</param>
        private static void SetupCallSequenceLogging(string appName, string time,
            ref LoggingConfiguration config)
        {
            var fileTarget = CreateBaseFileTarget(appName, time, CallSequenceLoggerSuffix);
            fileTarget.Layout = "${message}";
            fileTarget.ArchiveNumbering = ArchiveNumberingMode.Sequence;
            fileTarget.ArchiveAboveSize = 10 * 1024 * 1024;
            fileTarget.ArchiveEvery = FileArchivePeriod.Day;
            config.AddTarget(CallSequenceTargetName, fileTarget);
            config.LoggingRules.Add(new LoggingRule(
                CallSequenceLoggerSuffix, LogLevel.Trace, LogLevel.Trace, fileTarget));
        }

        /// <summary>
        /// For trace logging, create a file target keyed by the shortName (suffix) of the
        /// logger name. This enables us to have separate tracing sessions, potentially
        /// with multiple sessions running in parallel. Only accepts events from loggers
        /// with the |TraceLoggerPrefix| in the name and at Trace level.
        /// </summary>
        /// <param name="appName">log file prefix unique to this executable</param>
        /// <param name="time">time string to differentiate instances</param>
        private static void SetupTraceLogging(string appName, string time,
            ref LoggingConfiguration config)
        {
            var traceFileTarget = CreateBaseFileTarget($"{appName}.Trace", time,
                "${logger:shortName=true}");

            // TODO: look into deferring message-formatting for better performance
            traceFileTarget.Layout = "${message}";

            // Further performance optimizations. Keep files open for 30s at a time.
            // The timeout ensures that files for completed debug sessions are closed.
            traceFileTarget.KeepFileOpen = true;
            traceFileTarget.OpenFileCacheTimeout = 30;

            // Use async writes for trace logging to reduce overhead at the trace site.
            var asyncWrapper = new AsyncTargetWrapper(traceFileTarget);
            asyncWrapper.OverflowAction = AsyncTargetWrapperOverflowAction.Grow;

            config.AddTarget(TraceFileTargetName, traceFileTarget);
            config.LoggingRules.Add(
                new LoggingRule($"{TraceLoggerPrefix}.*", LogLevel.Trace, LogLevel.Trace,
                    asyncWrapper));
        }

        /// <summary>
        /// Creates a file target for log events using the appName prefix, followed by the suffixes
        /// joined with dots. Logs are written to the GGP SDK logs directory.
        /// </summary>
        /// <remarks>Log file names must be unique to this instance of this executable.</remarks>
        /// <param name="appName">prefix to uniquely identify this executable</param>
        /// <param name="suffixes">one or more strings to uniquely identify this log file</param>
        private static FileTarget CreateBaseFileTarget(string appName, params string[] suffixes)
        {
            if (suffixes.Length < 1)
            {
                throw new ArgumentException("need at least 1 suffix", nameof(suffixes));
            }
            var suffix = string.Join(".", suffixes);

            var logPath = SDKUtil.GetLoggingPath();
            var fileTarget = new FileTarget();
            fileTarget.FileName =
                string.Format("{0}/{1}.{2}.log", logPath, appName, suffix);
            // Set up an archive file name regardless if archiving will be used or not.
            // https://github.com/nlog/NLog/wiki/File-target#archival-options
            // Note that {{#}} gets converted to {#} in the formatted string, which is then
            // replaced by the archive number.
            fileTarget.ArchiveFileName =
                string.Format("{0}/{1}.{2}.{{#}}.log", logPath, appName, suffix);
            // Disable concurrent writes because we always use a unique file for each process.
            fileTarget.ConcurrentWrites = false;
            return fileTarget;
        }

        /// <summary>
        /// Returns the current general log file path.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when this hasn't been initialized.
        /// </exception>
        public static string CurrentLogFile
        {
            get
            {
                if (logFactory == null)
                {
                    throw new InvalidOperationException(
                        nameof(YetiLog) + " has not been initialized.");
                }
                var target =
                    (FileTarget)logFactory.Configuration.FindTargetByName(GeneralFileTargetName);
                return target.FileName.Render(LogEventInfo.CreateNullEvent());
            }
        }

        /// <summary>
        /// Converts |dateTime| to a format suitable for use in log file names with precision in
        /// seconds.
        /// </summary>
        public static string ToLogFileDateTime(DateTime dateTime)
            => dateTime.ToString("yyyyMMdd-HHmmss");

        /// <summary>
        /// Returns a logger to use for debug trace events associated with a specific key.
        /// All traces from the same key will go to the same log file. The log file name will
        /// contain the provided key.
        /// </summary>
        /// <param name="key">Log file name suffix; cannot contain dots</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when this hasn't been initialized.
        /// </exception>
        public static ILogger GetTraceLogger(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("null or empty", nameof(key));
            }
            if (key.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException("contains invalid file name chars", nameof(key));
            }
            if (logFactory == null)
            {
                throw new InvalidOperationException(nameof(YetiLog) + " has not been initialized.");
            }
            // The logger short name (suffix) is defined as the part after the last dot, so we
            // can't be introducing any dots into the key itself.
            if (key.IndexOf('.') >= 0)
            {
                throw new ArgumentException("contains a dot", nameof(key));
            }
            // The key becomes the shortname of the logger, which is part of the log file name.
            return logFactory.GetLogger($"{TraceLoggerPrefix}.{key}");
        }

        /// <summary>
        /// Returns a logger to use for call sequence traces.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when this hasn't been initialized.
        /// </exception>
        public static ILogger GetCallSequenceLogger()
        {
            if (logFactory == null)
            {
                throw new InvalidOperationException(nameof(YetiLog) + " has not been initialized.");
            }
            return logFactory.GetLogger(CallSequenceLoggerSuffix);
        }

        /// <summary>
        /// Gets the specified named logger.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// Thrown if name is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when this hasn't been initialized.
        /// </exception>
        public static ILogger GetLogger(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (logFactory == null)
            {
                throw new InvalidOperationException(nameof(YetiLog) + " has not been initialized.");
            }
            return logFactory.GetLogger(name);
        }

        static Grpc.Core.Logging.ILogger originalGrpcLogger = null;

        /// <summary>
        /// Toggles Grpc logging capture.
        ///
        /// Logs are captured to the YetiVSI log files.
        /// </summary>
        public static void ToggleGrpcLogging(bool enabled)
        {
            if (enabled && originalGrpcLogger == null)
            {
                originalGrpcLogger = Grpc.Core.GrpcEnvironment.Logger;
                Trace.WriteLine($"Enabling GrpcLogging.");
                Grpc.Core.GrpcEnvironment.SetLogger(new Grpc.Core.Logging.LogLevelFilterLogger(
                    new Cloud.GrpcLogger(), Grpc.Core.Logging.LogLevel.Debug));
            }
            else if (!enabled && originalGrpcLogger != null)
            {
                Trace.WriteLine($"Disabling GrpcLogging.");
                // Calling SetLogger(null) will raise an exception.
                Grpc.Core.GrpcEnvironment.SetLogger(originalGrpcLogger);
                originalGrpcLogger = null;
            }
            else
            {
                Trace.WriteLine($"WARNING: Could not toggle Grpc Logging. " +
                    $"Cannot set enabled={enabled} when originalGrpcLogger={originalGrpcLogger}");
            }
        }
    }
}

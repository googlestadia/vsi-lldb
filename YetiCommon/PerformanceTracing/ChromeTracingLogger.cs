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
using System.Collections.Generic;
using Newtonsoft.Json;

namespace YetiCommon.PerformanceTracing
{
    /// <summary>
    /// Receives events and sends them to an NLog logger as a JSON serialized string in
    /// the Chrome Tracing format as complete events. Output logs require post-processing.
    /// </summary>
    public class ChromeTracingLogger : ITracingLogger
    {
        /// <summary>
        /// Constants that are used by Chrome Tracing to determine what event type and
        /// phase each event is in.
        /// </summary>
        static class PhaseConstants
        {
            public const char Complete = 'X';
            public const char Metadata = 'M';
        }

        static class MetadataEventDescriptions
        {
            public const string ThreadNameName = "thread_name";
            public const string ThreadNameArg = "name";
        }

        static class AsyncEventDescriptions
        {
            public const int ThreadId = -1;
            public const long TraceTimestamp = -1;
            public const string NameArg = "async";
        }

        /// <summary>
        /// Internal representation of Chrome Tracing events.
        /// These properties are intentionally named to match syntax requirements of
        /// the Chrome Tracing file format.
        /// </summary>
        public struct Event
        {
            /// <summary>
            /// The displayed name of the event, used as an identifier when counting calls.
            /// </summary>
            [JsonProperty("name")]
            public string Name { get; set; }

            /// <summary>
            /// Duration of the event measured in microseconds. For Complete events.
            /// </summary>
            [JsonProperty("dur")]
            public long DurationUs { get; set; }

            /// <summary>
            /// Timestamp on microsecond time scale. The smallest timestamp is used as time 0.
            /// </summary>
            [JsonProperty("ts")]
            public long TimestampUs { get; set; }

            /// <summary>
            /// Thread ID.
            /// </summary>
            [JsonProperty("tid")]
            public int ThreadId { get; set; }

            /// <summary>
            /// Process ID.
            /// </summary>
            [JsonProperty("pid")]
            public int ProcessId { get; set; }

            /// <summary>
            /// Phase that the event is in. Some event types are represented by multiple events
            /// in different phases (i.e. and combo of begin 'b' event and an end 'e' event).
            /// </summary>
            [JsonProperty("ph")]
            public char Phase { get; set; }

            /// <summary>
            /// Category of the event. This is a mandatory attribute for flow events.
            /// </summary>
            [JsonProperty("cat")]
            public string Category { get; set; }

            /// <summary>
            /// Arguments used for metadata events.
            /// </summary>
            [JsonProperty("args")]
            public Dictionary<string, string> Args { get; set; }
        }

        readonly int _processId;
        readonly NLog.ILogger _logger;
        readonly Func<bool> _insideAsyncFunc;

        readonly JsonSerializerSettings _ignoreNullSettings;

        /// <param name="processId">Process ID.</param>
        /// <param name="logger">NLog logger instance.</param>
        /// <param name="insideAsyncFunc">AsyncLocal from TaskExecutor which determines if task is
        /// running inside async context.</param>
        public ChromeTracingLogger(int processId, NLog.ILogger logger, Func<bool> insideAsyncFunc)
        {
            _processId = processId;
            _logger = logger;
            _insideAsyncFunc = insideAsyncFunc;

            _ignoreNullSettings = new JsonSerializerSettings
                {DefaultValueHandling = DefaultValueHandling.Ignore};

            // Metadata event to name the async thread
            var asyncThreadMetadataArgs = new Dictionary<string, string>
            {
                {MetadataEventDescriptions.ThreadNameArg, AsyncEventDescriptions.NameArg}
            };

            TraceMetadataEvent(MetadataEventDescriptions.ThreadNameName,
                               AsyncEventDescriptions.TraceTimestamp,
                               AsyncEventDescriptions.ThreadId, asyncThreadMetadataArgs);
        }

        public void TraceEvent(string name, EventType eventType, Type callerType, long durationUs,
                               long timestampUs, int threadId)
        {
            string traceName = callerType?.FullName + "." + name;

            if (_insideAsyncFunc())
            {
                TraceEventInAsyncFiber(eventType, traceName, durationUs, timestampUs);
            }
            else
            {
                TraceEventInSyncFiber(traceName, durationUs, timestampUs, threadId);
            }
        }

        void TraceEventInSyncFiber(string traceName, long durationUs, long timestampUs,
                                   int threadId)
        {
            var traceEvent = new Event
            {
                Phase = PhaseConstants.Complete,
                Name = traceName,
                DurationUs = durationUs,
                TimestampUs = timestampUs,
                ThreadId = threadId,
                ProcessId = _processId
            };

            WriteTrace(traceEvent);
        }

        void TraceEventInAsyncFiber(EventType eventType, string traceName, long durationUs,
                                    long timestampUs)
        {
            var traceEvent = new Event
            {
                Phase = PhaseConstants.Complete,
                Name = traceName,
                DurationUs = durationUs,
                TimestampUs = timestampUs,
                ThreadId = AsyncEventDescriptions.ThreadId,
                ProcessId = _processId,
                Category = eventType.ToString()
            };

            WriteTrace(traceEvent);
        }

        public void TraceMetadataEvent(string traceName, long timestamp, int threadId,
                                       Dictionary<string, string> args)
        {
            var traceEvent = new Event
            {
                Phase = PhaseConstants.Metadata,
                Name = traceName,
                TimestampUs = timestamp,
                ThreadId = threadId,
                ProcessId = _processId,
                Args = args
            };

            WriteTrace(traceEvent);
        }

        void WriteTrace(Event traceEvent)
        {
            // TODO: look into deferring message-formatting for better performance
            var serializedTrace = JsonConvert.SerializeObject(traceEvent, _ignoreNullSettings);
            _logger.Trace(serializedTrace);
        }
    }
}
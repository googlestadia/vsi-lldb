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
using System.Threading.Tasks;
using GgpGrpc.Cloud.Interceptors;
using Grpc.Core;
using Metrics.Shared;

namespace YetiVSI.Metrics
{
    /// <summary>
    /// Represents an action that can be recorded as a metrics log event.
    ///</summary>
    ///<remarks>
    /// Actions record latency, outcome (success or error) and any other details that should be
    /// included in the log event. You must call one of the Record methods to build the final
    /// event proto and send the data to the Metrics service. An event can only be recorded once.
    ///</remarks>
    public interface IAction : RpcRecorder
    {
        /// <summary>
        /// Returns the contents of the log event so far. This is mostly for testing. Code that
        /// records events should use UpdateEvent and not care about the content of the event.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the action has already been recorded.
        /// </exception>
        DeveloperLogEvent GetEvent();

        /// <summary>
        /// Merges any fields set on this event into the event proto in a thread-safe manner.
        /// </summary>
        /// <remarks>
        /// There is no guarantee that every field is preserved in the final event. In particular,
        /// recording the action will update the latency and status code (only on failure*).
        /// *new code should not rely on this behavior.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the action has already been recorded.
        /// </exception>
        /// <returns>This action to support chaining.</returns>
        IAction UpdateEvent(DeveloperLogEvent logEvent);

        /// <summary>
        /// Runs the given action, records its latency, and records the event.
        /// If the action throws an exception, record an error status and re-throw.
        /// </summary>
        /// <param name="actionFunc">The action to execute synchronously.</param>
        /// <remarks>
        /// The execution of actionFunc is not synchronized, and UpdateEvent may be used inside.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the action has already been records.
        /// </exception>
        void Record(System.Action actionFunc);

        /// <summary>
        /// Performs the action, records its status, and records the event.
        /// If the actionFunc returns true, record succes, otherwise record cancelled.
        /// If the action throws an exception, record an error status and re-throw.
        /// </summary>
        /// <param name="actionFunc">The action to run synchronously.</param>
        /// <returns>The return value of actionFunc.</returns>
        /// <remarks>
        /// The execution of actionFunc is not synchronized, and UpdateEvent may be used inside.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the action has already been records.
        /// </exception>
        bool Record(Func<bool> actionFunc);

        /// <summary>
        /// Returns a task that performs the actionTask, records its status, and records the event.
        /// If the actionTask finishes, return true; otherwise record is cancelled.
        /// If the actionTask throws an exception, record an error status and re-throw.
        /// </summary>
        /// <param name="actionTask">The task to execute asynchronously.</param>
        /// <remarks>
        /// The execution of actionTask is not synchronized, and UpdateEvent may be used inside.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the action has already been records.
        /// </exception>
        Task RecordAsync(Task actionTask);
    }

    /// <summary>
    /// An action that doesn't do any recording.
    /// </summary>
    public class DummyAction : IAction
    {
        void RpcRecorder.Record(IMethod method, Status status, long latencyMilliseconds) { }

        public DeveloperLogEvent GetEvent() { return null; }

        public IAction UpdateEvent(DeveloperLogEvent logEvent) { return this; }

        public void Record(System.Action actionFunc) { actionFunc(); }

        public bool Record(Func<bool> actionFunc) { return actionFunc(); }

#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
        public async Task RecordAsync(Task actionTask) { await actionTask; }
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks
    }

    /// <summary>
    /// Implementation of IAction used by the ActionRecorder.
    /// </summary>
    public class Action : IAction
    {
        static long MicrosPerMillis = 1000;

        readonly DeveloperEventType.Types.Type eventType;
        readonly Timer.Factory timerFactory;
        readonly IVsiMetrics metrics;
        readonly object logEventLock = new object();

        // Event for recording extra information during an action, protected by eventLock.
        DeveloperLogEvent logEvent;

        #region IAction

        public DeveloperLogEvent GetEvent()
        {
            lock(logEventLock)
            {
                CheckValid();
                return logEvent.Clone();
            }
        }

        public IAction UpdateEvent(DeveloperLogEvent logEvent)
        {
            lock (logEventLock)
            {
                CheckValid();
                this.logEvent.MergeFrom(logEvent);
                return this;
            }
        }

        public async Task RecordAsync(Task actionTask)
        {
            CheckValid();
            var timer = timerFactory.CreateStarted();
            try
            {
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
                await actionTask;
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks
            }
            catch (Exception e) when (CheckRecordException(e))
            {
                Debug.Fail("Exception should never be caught");
                throw;
            }
            finally
            {
                timer.Stop();
                Flush(new DeveloperLogEvent { LatencyMilliseconds = timer.ElapsedMilliseconds });
            }
        }

        public void Record(System.Action actionFunc)
        {
            Record(() => { actionFunc.Invoke(); return true; });
        }

        public bool Record(Func<bool> actionFunc)
        {
            CheckValid();
            var timer = timerFactory.CreateStarted();
            try
            {
                bool completed = actionFunc.Invoke();
                if (!completed)
                {
                    UpdateEvent(new DeveloperLogEvent
                    {
                        StatusCode = DeveloperEventStatus.Types.Code.Cancelled
                    });
                }
                return completed;
            }
            catch (Exception e) when (CheckRecordException(e))
            {
                Debug.Fail("Exception should never be caught");
                throw;
            }
            finally
            {
                timer.Stop();
                Flush(new DeveloperLogEvent
                {
                    LatencyMilliseconds = timer.ElapsedMilliseconds
                });
            }
        }

        #endregion

        // Create a valid action of the given type. This is an internal constructor used by
        // ActionRecorder. It is public for use in tests only, where ActionRecorder is not
        // the right dependency to inject.
        public Action(DeveloperEventType.Types.Type eventType,
            Timer.Factory timerFactory, IVsiMetrics metrics)
        {
            this.eventType = eventType;
            logEvent = new DeveloperLogEvent
            {
                StatusCode = DeveloperEventStatus.Types.Code.Success
            };
            this.timerFactory = timerFactory;
            this.metrics = metrics;
        }

        // Record a successful instantaneous event.
        internal void RecordSuccess(DeveloperLogEvent details)
        {
            Flush(details);
        }

        // Record an error inside an instantaneous event.
        internal void RecordFailure(Exception e, DeveloperLogEvent details)
        {
            DeveloperLogEvent evt = ExceptionHelper.RecordException(e);
            evt.MergeFrom(details);
            Flush(evt);
        }

        // Records an RPC as part of this action. DOES NOT flush the action. Thread-safe.
        void RpcRecorder.Record(IMethod method, Status status, long latencyMilliseconds)
        {
            var evt = new DeveloperLogEvent();
            evt.GrpcCallDetails = new List<GrpcServiceCallDetails>();
            evt.GrpcCallDetails.Add(new GrpcServiceCallDetails
            {
                ServiceName = method.ServiceName,
                ServiceMethod = method.Name,
                Status = status,
                RoundtripLatency = latencyMilliseconds * MicrosPerMillis
            });
            UpdateEvent(evt);
        }

        // Update event with details, build the final event proto, invalidate this action,
        // and record the event.
        void Flush(DeveloperLogEvent details)
        {
            DeveloperLogEvent eventProto;
            lock (logEventLock)
            {
                CheckValid();
                try
                {
                    logEvent.MergeFrom(details);
                    eventProto = logEvent.Clone();
                }
                finally
                {
                    logEvent = null;
                }
            }
            metrics.RecordEvent(eventType, eventProto);
        }

        bool CheckRecordException(Exception e)
        {
            UpdateEvent(ExceptionHelper.RecordException(e));
            return false;
        }

        void CheckValid()
        {
            if (logEvent == null)
            {
                throw new InvalidOperationException("The Action has already been recorded.");
            }
        }
    }
}

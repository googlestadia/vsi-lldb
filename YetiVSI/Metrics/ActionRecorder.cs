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
using Metrics.Shared;

namespace YetiVSI.Metrics
{
    /// <summary>
    /// Records information about actions and logs the resulting DeveloperLogEvent.
    /// </summary>
    /// <remarks>
    /// There are two main categories of actions that we are interested in recording:
    ///   - Tool actions are blocking work done by the VSI that the user is waiting for.
    ///   - User actions are dialogs and prompts where the VSI is waiting for user input.
    /// </remarks>
    public class ActionRecorder
    {
        readonly IMetrics metrics;
        readonly Timer.Factory timerFactory;

        /// <summary>
        /// Creates an action recorder that sends events to the given event recorder.
        /// </summary>
        /// <param name="metrics"></param>
        public ActionRecorder(IMetrics metrics)
            : this(metrics, new Timer.Factory())
        { }

        /// <summary>
        /// Create an action recorder with an explicit timer factory. For use in testing.
        /// </summary>
        public ActionRecorder(IMetrics metrics, Timer.Factory timerFactory)
        {
            this.metrics = metrics;
            this.timerFactory = timerFactory;
        }

        /// <summary>
        /// Create and start a timer that can be used for recording arbitrary time intervals as
        /// actions via RecordToolAction.
        /// </summary>
        /// <returns>A timer that is in the started state.</returns>
        public ITimer CreateStartedTimer()
        {
            return timerFactory.CreateStarted();
        }

        /// <summary>
        /// Creates a new Action for the given type.
        /// The resulting action can be used to record work that the user is waiting for.
        /// Use one of its Record methods to record some work for this action.
        /// </summary>
        /// <param name="type">The type of action to record.</param>
        // TODO: create interface instead of declaring method as virtual
        // to be able to mock in tests.
        public virtual IAction CreateToolAction(ActionType type)
        {
            return CreateAction(type)
                .UpdateEvent(new DeveloperLogEvent
                {
                    LatencyType = DeveloperLogEvent.Types.LatencyType.LatencyTool
                });
        }

        /// <summary>
        /// Runs an action that is blocked on the user and record its latency.
        /// canceled. Records status SUCCESS or CANCELLED respectively.
        /// If the action throws an exception, record an error status and re-throw.
        /// </summary>
        /// <param name="type">The type of action to record.</param>
        /// <param name="actionFunc">The action to execute synchronously.</param>
        /// <returns>The return value of actionFunc.</returns>
        public bool RecordUserAction(ActionType type, Func<bool> actionFunc)
        {
            return CreateAction(type)
                .UpdateEvent(new DeveloperLogEvent
                {
                    LatencyType = DeveloperLogEvent.Types.LatencyType.LatencyUser
                })
                .Record(actionFunc);
        }

        /// <summary>
        /// Runs the given action that the user is waiting for and records its latency.
        /// If the action throws an exception, record an error status and re-throw.
        /// </summary>
        /// <param name="type">The type of action to record.</param>
        /// <param name="actionFunc">The action to execute synchronously.</param>
        public void RecordToolAction(ActionType type, System.Action actionFunc)
        {
            CreateToolAction(type).Record(actionFunc);
        }

        /// <summary>
        /// Records latency of an action based on the time elapsed on the timer.
        /// </summary>
        /// <param name="type">The type of action to record.</param>
        /// <param name="actionTimer">The timer representing the duration of the action.</param>
        public void RecordToolAction(ActionType type, ITimer actionTimer,
            DeveloperLogEvent details = null)
        {
            var logEvent = new DeveloperLogEvent
            {
                LatencyType = DeveloperLogEvent.Types.LatencyType.LatencyTool,
                LatencyMilliseconds = actionTimer.ElapsedMilliseconds
            };
            logEvent.MergeFrom(details);

            CreateAction(type).RecordSuccess(logEvent);
        }

        /// <summary>
        /// Records a successful instantaneous event.
        /// </summary>
        /// <param name="type">The type of action to record.</param>
        /// <param name="details">Optional details to merge into the event proto.</param>
        public void RecordSuccess(ActionType type, DeveloperLogEvent details = null)
        {
            CreateAction(type).RecordSuccess(details);
        }

        /// <summary>
        /// Record an error inside an instantaneous event.
        /// </summary>
        /// <param name="type">The type of action to record.</param>
        /// <param name="e">The error to be recorded.</param>
        /// <param name="details">Optional details to merge into the event proto.</param>
        public void RecordFailure(ActionType type, Exception e, DeveloperLogEvent details = null)
        {
            CreateAction(type).RecordFailure(e, details);
        }

        Action CreateAction(ActionType type) =>
            new Action(ActionTypeMapping.ActionToEventType(type), timerFactory, metrics);
    }
}

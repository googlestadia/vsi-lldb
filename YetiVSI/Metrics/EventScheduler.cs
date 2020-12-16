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

﻿using System.Threading;

namespace YetiVSI.Metrics
{
    /// <summary>
    /// Simple interface to schedule events.
    /// </summary>
    public interface IEventScheduler
    {
        /// <summary>
        /// Restart (or start) the scheduler.
        /// </summary>
        /// <param name="timeout">The amount of time to delay before invoking the callback,
        /// in milliseconds.</param>
        void Restart(int timeout);
    }

    /// <summary>
    /// Simple interface of a factory that creates IEventScheduler values.
    /// </summary>
    public interface IEventSchedulerFactory
    {
        /// <summary>
        /// Create an IEventScheduler in the stopped state.
        /// </summary>
        /// <remarks>
        /// The callback will be invoked on a thread pool thread.
        /// </remarks>
        IEventScheduler Create(System.Action callback);
    }

    /// <summary>
    /// Implementation of IEventScheduler using System.Threading.Timer.
    /// </summary>
    public class EventScheduler : IEventScheduler
    {
        public class Factory : IEventSchedulerFactory
        {
            public IEventScheduler Create(System.Action callback) => new EventScheduler(callback);
        }

        private readonly System.Threading.Timer timer;

        private EventScheduler(System.Action callback)
        {
            timer = new System.Threading.Timer(_ => callback?.Invoke());
        }

        public void Restart(int timeout) => timer.Change(timeout, Timeout.Infinite);
    }
}

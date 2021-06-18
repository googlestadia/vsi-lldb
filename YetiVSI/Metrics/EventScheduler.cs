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

namespace YetiVSI.Metrics
{
    /// <summary>
    /// Simple interface to schedule events.
    /// </summary>
    public interface IEventScheduler
    {
        /// <summary>
        /// Enable the event scheduler with interval provided at the constructor. This method
        /// should be invoked when a new batch is created.
        /// </summary>
        void Enable();

        /// <summary>
        /// Disable the event scheduler. This method should be invoked when the current batch is
        /// ready and sent.
        /// </summary>
        void Disable();
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
        IEventScheduler Create(System.Action callback, int interval);
    }

    /// <summary>
    /// Implementation of IEventScheduler using System.Threading.Timer.
    /// </summary>
    public class EventScheduler : IEventScheduler
    {
        public class Factory : IEventSchedulerFactory
        {
            public IEventScheduler Create(System.Action callback, int interval) =>
                new EventScheduler(callback, interval);
        }

        readonly System.Threading.Timer _timer;
        readonly int _interval;

        EventScheduler(System.Action callback, int interval)
        {
            _timer = new System.Threading.Timer(_ => callback?.Invoke());
            _interval = interval;
        }

        public void Enable() => _timer.Change(_interval, _interval);

        public void Disable() => _timer.Change(0, 0);
    }
}

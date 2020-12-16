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
using System.Timers;

namespace Google.VisualStudioFake.Internal.Jobs
{
    /// <summary>
    /// Job that notifies a callback when some condition becomes true.
    /// </summary>
    /// <remarks>
    /// The job will continue to reschedule itself until the condition becomes true.
    /// </remarks>
    public class ObserveAndNotifyJob : IJob
    {
        public class Factory
        {
            readonly IJobQueue _jobQueue;

            public Factory(IJobQueue jobQueue)
            {
                _jobQueue = jobQueue;
            }

            public ObserveAndNotifyJob Create(Func<bool> predicate, Action callback) =>
                new ObserveAndNotifyJob(_jobQueue, predicate, callback);

            public ObserveAndNotifyJob Create(
                Func<bool> predicate, Action callback, string logDetails) =>
                    new ObserveAndNotifyJob(_jobQueue, predicate, callback, logDetails);
        }

        const double POLL_INTERVAL_MS = 100;

        readonly IJobQueue _jobQueue;
        readonly Func<bool> _predicate;
        readonly Action _callback;
        readonly string _logDetails;

        readonly Timer _timer;

        public ObserveAndNotifyJob(IJobQueue jobQueue, Func<bool> predicate, Action callback)
            : this(jobQueue, predicate, callback, null) { }

        public ObserveAndNotifyJob(IJobQueue jobQueue, Func<bool> predicate, Action callback,
            string logDetails)
        {
            _jobQueue = jobQueue;
            _predicate = predicate;
            _callback = callback;
            _logDetails = logDetails ?? "";

            _timer = new Timer(POLL_INTERVAL_MS);
            _timer.AutoReset = false;
            _timer.Elapsed += TimerExpired;
        }

#region IJob

        public void Run()
        {
            if (_predicate())
            {
                _callback();
                return;
            }
            _timer.Start();
        }

        public string GetLogDetails()
        {
            return $"\"{_logDetails}\"";
        }

#endregion

        void TimerExpired(object sender, ElapsedEventArgs e)
        {
            _jobQueue.Push(this);
        }
    }
}

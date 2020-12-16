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
using System.Reflection;

namespace YetiVSI.Metrics
{
    /// <summary>
    /// Aggregates debug events into different batches and raises an event when a certain batch
    /// is ready to be uploaded.
    /// </summary>
    public interface IDebugEventAggregator
    {
        /// <summary>
        /// Adds a new debug event. This call should not perform any CPU-intensive task.
        /// </summary>
        /// <param name="methodInfo">Information about the callee method.</param>
        /// <param name="initialTimestampInMicro">Timestamp just before the method was called,
        /// in microseconds.</param>
        /// <param name="finalTimestampInMicro">Timestamp immediately after the method finishes
        /// executing, in microseconds.</param>
        void Add(MethodInfo methodInfo, long initialTimestampInMicro, long finalTimestampInMicro);

        /// <summary>
        /// This event will be raised each time a new debug event batch summary is ready.
        /// </summary>
        event EventHandler<DebugEventBatchSummary> BatchSummaryReady;
    }

    /// <summary>
    /// Thread-safe implementation of <see cref="IDebugEventAggregator"/>.
    /// </summary>
    public class DebugEventAggregator : IDebugEventAggregator
    {
        public event EventHandler<DebugEventBatchSummary> BatchSummaryReady;

        private readonly DebugEventBatch.Factory batchFactory;
        private readonly int minimumBatchSeparationInMilliseconds;
        private readonly IEventScheduler scheduler;

        private readonly object currentBatchAndTimerLocker;
        private IDebugEventBatch currentBatch;
        private readonly ITimer timer;

        public DebugEventAggregator(DebugEventBatch.Factory batchFactory,
            int minimumBatchSeparationInMilliseconds,
            IEventSchedulerFactory schedulerFactory, ITimer timer)
        {
            this.batchFactory = batchFactory;
            this.minimumBatchSeparationInMilliseconds = minimumBatchSeparationInMilliseconds;
            scheduler = schedulerFactory.Create(HandleBatchCheck);

            currentBatchAndTimerLocker = new object();
            currentBatch = batchFactory.Create();
            this.timer = timer;
        }

        public void Add(
            MethodInfo methodInfo, long initialTimestampInMicro, long finalTimestampInMicro)
        {
            lock (currentBatchAndTimerLocker)
            {
                // These expressions are guarded by the lock so we don't have the following:
                // 1 - ::HandleBatchCheck() verifies that timer.ElapsedMilliseconds >=
                // MinimumBatchSeparationInMilliseconds.
                // 2 - Add(.) gets called and inserts event E.
                // 3 Previous ::HandleBatchCheck() call invokes the event BatchSummaryReady
                // with event E added.
                // Additionally, IDebugEventBatch::Add is not thread-safe currently.
                currentBatch.Add(methodInfo, initialTimestampInMicro, finalTimestampInMicro);
                timer.Restart();
            }
            scheduler.Restart(minimumBatchSeparationInMilliseconds);
        }

        private void HandleBatchCheck()
        {
            IDebugEventBatch finalizedBatch;
            lock (currentBatchAndTimerLocker)
            {
                if (timer.ElapsedMilliseconds < minimumBatchSeparationInMilliseconds)
                {
                    return;
                }

                finalizedBatch = currentBatch;
                currentBatch = batchFactory.Create();
                timer.Reset();
            }
            BatchSummaryReady?.Invoke(this, finalizedBatch.GetSummary());
        }
    }
}

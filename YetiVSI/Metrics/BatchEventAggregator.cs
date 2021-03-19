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

namespace YetiVSI.Metrics
{
    /// <summary>
    /// Aggregates events into different batches and raises an event when a certain batch is ready
    /// to be uploaded.
    /// </summary>
    public interface IBatchEventAggregator<TParams, TSummary>
    {
        /// <summary>
        /// Adds a new event to a batch. This call should not perform any CPU-intensive task.
        /// </summary>
        /// <param name="batchParams">Specific parameters according to the batch events to be
        /// collected.</param>
        void Add(TParams batchParams);

        /// <summary>
        /// This event will be raised each time a new event batch summary is ready.
        /// </summary>
        event EventHandler<TSummary> BatchSummaryReady;
    }

    /// <summary>
    /// Thread-safe implementation of <see cref="IBatchEventAggregator{TParams,TSummary}"/>.
    /// </summary>
    public class BatchEventAggregator<TBatch, TParams, TSummary> : IBatchEventAggregator<TParams, TSummary>
        where TBatch : IEventBatch<TParams, TSummary>, new()
    {
        public event EventHandler<TSummary> BatchSummaryReady;

        readonly int _minimumBatchSeparationInMilliseconds;
        readonly IEventScheduler _scheduler;

        readonly object _currentBatchAndTimerLocker;
        IEventBatch<TParams, TSummary> _currentBatch;
        readonly ITimer _timer;

        public BatchEventAggregator(int minimumBatchSeparationInMilliseconds,
                                    IEventSchedulerFactory schedulerFactory, ITimer timer)
        {
            _minimumBatchSeparationInMilliseconds = minimumBatchSeparationInMilliseconds;
            _scheduler = schedulerFactory.Create(HandleBatchCheck);

            _currentBatchAndTimerLocker = new object();
            _currentBatch = new TBatch();
            _timer = timer;
        }

        public void Add(TParams batchParams)
        {
            lock (_currentBatchAndTimerLocker)
            {
                // These expressions are guarded by the lock so we don't have the following:
                // 1 - ::HandleBatchCheck() verifies that timer.ElapsedMilliseconds >=
                // MinimumBatchSeparationInMilliseconds.
                // 2 - Add(.) gets called and inserts event E.
                // 3 Previous ::HandleBatchCheck() call invokes the event BatchSummaryReady
                // with event E added.
                // Additionally, IEventBatch::Add is not thread-safe currently.
                _currentBatch.Add(batchParams);
                _timer.Restart();
            }
            _scheduler.Restart(_minimumBatchSeparationInMilliseconds);
        }

        void HandleBatchCheck()
        {
            IEventBatch<TParams, TSummary> finalizedBatch;
            lock (_currentBatchAndTimerLocker)
            {
                if (_timer.ElapsedMilliseconds < _minimumBatchSeparationInMilliseconds)
                {
                    return;
                }

                finalizedBatch = _currentBatch;
                _currentBatch = new TBatch();
                _timer.Reset();
            }
            BatchSummaryReady?.Invoke(this, finalizedBatch.GetSummary());
        }
    }
}

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
using System.Linq;
using Priority_Queue;
using System.Diagnostics;

namespace Google.VisualStudioFake.Internal.Jobs
{
    public class JobQueue : IJobQueue
    {
        // This is a thread-safe priority queue.
        SimplePriorityQueue<IJob> queue;

        public JobQueue()
        {
            queue = new SimplePriorityQueue<IJob>();
        }

        public IEnumerable<IJob> Pop(Predicate<IJob> predicate)
        {
            lock (queue)
            {
                IEnumerable<IJob> matchedJobs = queue.Where(job => predicate(job));
                foreach (IJob job in matchedJobs)
                {
                    queue.Remove(job);
                }
                return matchedJobs;
            }
        }

        public bool Pop(out IJob job)
        {
            lock (queue)
            {
                var result = queue.TryDequeue(out job);
                if (result)
                {
                    Trace.WriteLine($"VSFake: Popping job {{queueSize:{queue.Count}, " +
                        $"type=\"{job.GetType()}\"}}");
                }
                return result;
            }
        }

        public void Push(IJob job)
        {
            lock (queue)
            {
                queue.Enqueue(job, 1);
                Trace.WriteLine($"VSFake: Queued job {{queueSize:{queue.Count}, " +
                    $"type:\"{job.GetType()}\", details:{job.GetLogDetails()}}}");
                // TODO: (internal) figure out the priority before enqueuing the job
            }
        }

        public bool Empty
        {
            get
            {
                lock (queue)
                {
                    return queue.Count == 0;
                }
            }
        }
    }
}

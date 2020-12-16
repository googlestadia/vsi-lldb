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

﻿using System.Diagnostics;

namespace Google.VisualStudioFake.Internal.Jobs
{
    public class JobExecutor
    {
        int _curJobId = 0;

        class JobData
        {
            public JobData(IJob job, int jobId)
            {
                Job = job;
                JobId = jobId;
            }

            public IJob Job { get; }
            public int JobId { get; set; }
            public Stopwatch Stopwatch { get; } = new Stopwatch();
        }

        public void Execute(IJob job)
        {
            var jobData = new JobData(job, ++_curJobId);
            Setup(jobData);
            job.Run();
            Cleanup(jobData);
        }

        void Setup(JobData jobData)
        {
            Trace.WriteLine($"VSFake: Starting to execute job {{id:{jobData.JobId}, " +
                $"type:\"{jobData.Job.GetType()}\" details:{{{jobData.Job.GetLogDetails()}}}}}.");
            jobData.Stopwatch.Start();
        }

        void Cleanup(JobData jobData)
        {
            jobData.Stopwatch.Stop();
            Trace.WriteLine($"VSFake: Finished executing job {{id:{jobData.JobId}, " +
                $"type:\"{jobData.Job.GetType()}\", " +
                $"duration:{jobData.Stopwatch.ElapsedMilliseconds}}}.");
        }
    }
}

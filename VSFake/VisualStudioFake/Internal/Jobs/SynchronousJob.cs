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

﻿using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Debugger.Interop;
using System.Threading.Tasks;

namespace Google.VisualStudioFake.Internal.Jobs
{
    /// <summary>
    /// Base class for all Jobs that handle a Synchronous event.
    /// </summary>
    public abstract class SynchronousJob : IJob
    {
        public class Factory
        {
            protected JoinableTaskContext taskContext;

            public Factory(JoinableTaskContext taskContext)
            {
                this.taskContext = taskContext;
            }
        }

        protected JoinableTaskContext taskContext;
        protected IDebugEvent2 evnt;
        protected IDebugEngine2 debugEngine;

        public SynchronousJob(JoinableTaskContext taskContext,
            IDebugEngine2 debugEngine, IDebugEvent2 evnt)
        {
            this.taskContext = taskContext;
            this.debugEngine = debugEngine;
            this.evnt = evnt;
        }

        /// <summary>
        /// The inheriting class override this with job specific tasks.
        /// </summary>
        protected abstract void RunJobTasks();

        public void Run()
        {
            RunJobTasks();
            ContinueFromSynchronousEvent();
        }

        public virtual string GetLogDetails()
        {
            return $"{{eventType:\"{evnt.GetType()}\"}}";
        }

        protected void ContinueFromSynchronousEvent()
        {
            taskContext.Factory.Run(async () =>
            {
                await taskContext.Factory.SwitchToMainThreadAsync();
                var result = debugEngine.ContinueFromSynchronousEvent(evnt);
                HResultChecker.Check(result);
            });
        }
    }
}

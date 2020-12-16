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

﻿using Microsoft.VisualStudio.Debugger.Interop;
using System;

namespace Google.VisualStudioFake.Internal.Jobs
{
    /// <summary>
    /// Base class for jobs whose sole purpose is to run an action.
    /// </summary>
    // TODO: We should review current approach for Generic Jobs.
    public abstract class GenericJob : IJob
    {
        protected readonly Action _action;
        private readonly string _logDetails;

        public GenericJob(Action action) : this(action, "") { }

        public GenericJob(Action action, string logDetails)
        {
            _action = action;
            _logDetails = logDetails ?? "";
        }

        public void Run() => _action();

        public virtual string GetLogDetails()
        {
            return $"\"{_logDetails}\"";
        }
    }

    public class BindBreakpointJob : GenericJob
    {
        public class Factory
        {
            public BindBreakpointJob Create(Action action) => new BindBreakpointJob(action);
        }

        public BindBreakpointJob(Action action) : base(action) { }
    }

    public class LaunchAndAttachJob : GenericJob
    {
        public class Factory
        {
            public LaunchAndAttachJob Create(Action action) => new LaunchAndAttachJob(action);
        }

        public LaunchAndAttachJob(Action action) : base(action) { }
    }

    public class BroadcastDebugEventJob : GenericJob
    {
        public class Factory
        {
            public BroadcastDebugEventJob Create(Action action, IDebugEvent2 @event) =>
                new BroadcastDebugEventJob(action, @event);
        }

        readonly IDebugEvent2 _event;

        public BroadcastDebugEventJob(Action action, IDebugEvent2 @event) : base(action)
        {
            _event = @event;
        }

        public override string GetLogDetails()
        {
            return $"{{eventType:\"{_event.GetType()}\"}}";
        }
    }

    public class RefreshVariableJob : GenericJob
    {
        public class Factory
        {
            public RefreshVariableJob Create(Action action) =>
                new RefreshVariableJob(action);
        }

        public RefreshVariableJob(Action action) : base(action) { }
    }

    public class BreakpointDeleteJob : GenericJob
    {
        public class Factory
        {
            public BreakpointDeleteJob Create(Action action, string breakpointDescription) =>
                new BreakpointDeleteJob(action, breakpointDescription);
        }

        public BreakpointDeleteJob(Action action, string breakpointDescription)
            : base(action, breakpointDescription) { }
    }
}

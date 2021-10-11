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

namespace Google.VisualStudioFake.Internal.Jobs
{
    /// <summary>
    /// Generic class for jobs whose sole purpose is to run an action.
    /// </summary>
    public class GenericJob : IJob
    {
        protected readonly Action _action;
        readonly string _logDetails;

        public GenericJob(Action action) : this(action, action.Method.Name) { }

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
}

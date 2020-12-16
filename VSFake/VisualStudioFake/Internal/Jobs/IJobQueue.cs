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

namespace Google.VisualStudioFake.Internal.Jobs
{
    public interface IJobQueue
    {
        bool Empty { get; }

        void Push(IJob job);

        bool Pop(out IJob job);

        /// <summary>
        /// Pops all jobs matching the given predicate.
        /// </summary>
        /// <param name="predicate">Predicate for jobs to be popped</param>
        /// <returns>A collection of matching jobs</returns>
        IEnumerable<IJob> Pop(Predicate<IJob> predicate);
    }
}

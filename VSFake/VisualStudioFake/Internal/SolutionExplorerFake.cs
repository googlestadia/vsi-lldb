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
using YetiCommon.Util;
using YetiCommon.VSProject;

namespace Google.VisualStudioFake.Internal
{
    public class SolutionExplorerFake : ISolutionExplorer
    {
        ISolutionExplorerProject project;

        public void HandleProjectLoaded(ISolutionExplorerProject project) => this.project = project;

        public IEnumerable<ISolutionExplorerProject> EnumerateProjects()
        {
            if (project == null)
            {
                throw new InvalidOperationException("No project has been loaded yet.");
            }

            return EnumerableHelpers.EnumerableOf(project);
        }
    }
}

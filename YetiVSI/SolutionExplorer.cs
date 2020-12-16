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

using Microsoft.VisualStudio.VCProjectEngine;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.Threading;
using YetiCommon.VSProject;
using YetiVSI.Util;

namespace YetiVSI
{
    public class SolutionExplorer : ISolutionExplorer
    {
        public class Factory
        {
            readonly VcProjectAdapter.Factory vcProjectAdapterFactory;

            public Factory(VcProjectAdapter.Factory vcProjectAdapterFactory)
            {
                this.vcProjectAdapterFactory = vcProjectAdapterFactory;
            }

            public virtual SolutionExplorer Create(
                JoinableTaskContext taskContext, IEnvDteUtil envDteUtil)
            {
                return new SolutionExplorer(taskContext, vcProjectAdapterFactory, envDteUtil);
            }
        }

        readonly JoinableTaskContext taskContext;
        readonly VcProjectAdapter.Factory projectFactory;
        readonly IEnvDteUtil envDteUtil;

        public SolutionExplorer(JoinableTaskContext taskContext,
            VcProjectAdapter.Factory projectFactory, IEnvDteUtil envDteUtil)
        {
            this.taskContext = taskContext;
            this.projectFactory = projectFactory;
            this.envDteUtil = envDteUtil;
        }

        public IEnumerable<ISolutionExplorerProject> EnumerateProjects()
        {
            taskContext.ThrowIfNotOnMainThread();

            foreach (var project in envDteUtil.GetSolutionProjects())
            {
                var vcProject = project as VCProject ?? project.Object as VCProject;
                if (vcProject == null)
                {
                    Trace.WriteLine(
                        "Unable to cast from EnvDTE.Project.Object to VCProject failed." +
                        $" Project: {project.Name} Kind: {project.Kind}");
                    continue;
                }
                var projectGgp = projectFactory.Create(vcProject);
                if (projectGgp == null)
                {
                    continue;
                }
                yield return projectGgp;
            }
        }
    }
}

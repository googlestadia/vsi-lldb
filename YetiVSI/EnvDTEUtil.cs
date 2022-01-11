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

using EnvDTE;
using EnvDTE80;
using System.Collections.Generic;
using Microsoft.VisualStudio.Threading;
using YetiVSI.Util;

namespace YetiVSI
{
    // Provides utility functions for working with the EnvDTE VisalStudio core automation library.
    public interface IEnvDteUtil
    {
        // Returns a list of all projects that are part of the currently opened solution.
        IList<Project> GetSolutionProjects();
    }

    public class EnvDteUtil : IEnvDteUtil
    {
        readonly JoinableTaskContext taskContext;
        readonly DTE2 dte2;

        public EnvDteUtil(JoinableTaskContext taskContext, DTE2 dte2)
        {
            this.taskContext = taskContext;
            this.dte2 = dte2;
        }

        public IList<Project> GetSolutionProjects()
        {
            taskContext.ThrowIfNotOnMainThread();

            var projects = dte2.Solution.Projects;
            var results = new List<Project>();
            foreach (var item in projects)
            {
                AddProjectAndSubprojects(results, item as Project);
            }
            return results;
        }

        void AddProjectAndSubprojects(List<Project> projects, Project project)
        {
            taskContext.ThrowIfNotOnMainThread();

            if (project == null)
            {
                return;
            }
            if (project.Kind == ProjectKinds.vsProjectKindSolutionFolder)
            {
                foreach (var item in project.ProjectItems)
                {
                    AddProjectAndSubprojects(projects, (item as ProjectItem).SubProject);
                }
            }
            else
            {
                projects.Add(project);
            }
        }
    }
}
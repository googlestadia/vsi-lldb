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
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using Microsoft.VisualStudio.Threading;

namespace YetiVSI.Test
{
    [TestFixture]
    class EnvDTEUtilTests
    {
        readonly DTE2 dte2 = Substitute.For<DTE2>();
#pragma warning disable VSSDK005 // Avoid instantiating JoinableTaskContext
        readonly JoinableTaskContext taskContext = new JoinableTaskContext();
#pragma warning restore VSSDK005 // Avoid instantiating JoinableTaskContext

        [Test]
        public void GetSolutionProjectsNull()
        {
            var solution = Substitute.For<Solution>();
            dte2.Solution.Returns(solution);
            var projects = new List<Project>();
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
            solution.Projects.GetEnumerator().Returns(projects.GetEnumerator());
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread
            var envDteUtil = new EnvDteUtil(taskContext, dte2);
            var results = envDteUtil.GetSolutionProjects();
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void GetSolutionProjectsNullProject()
        {
            var solution = Substitute.For<Solution>();
            Project nullProject = null;
            dte2.Solution.Returns(solution);
            var projects = new List<Project> { nullProject };
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
            solution.Projects.GetEnumerator().Returns(projects.GetEnumerator());
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread
            var envDteUtil = new EnvDteUtil(taskContext, dte2);
            var results = envDteUtil.GetSolutionProjects();
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void GetSolutionProjectsOneProject()
        {
            var solution = Substitute.For<Solution>();
            dte2.Solution.Returns(solution);
            Project project = Substitute.For<Project>();
            var solutionProjects = new List<Project> { project };
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
            solution.Projects.GetEnumerator().Returns(solutionProjects.GetEnumerator());
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread
            var envDteUtil = new EnvDteUtil(taskContext, dte2);
            var results = envDteUtil.GetSolutionProjects();
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(project, results[0]);
        }

        [Test]
        public void GetSolutionProjectsFolder()
        {
            var solution = Substitute.For<Solution>();
            Project project = Substitute.For<Project>();
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
            project.Kind.Returns(ProjectKinds.vsProjectKindSolutionFolder);
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread
            dte2.Solution.Returns(solution);
            var solutionProjects = new List<Project> { project };
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
            solution.Projects.GetEnumerator().Returns(solutionProjects.GetEnumerator());
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread
            var projectItem = Substitute.For<ProjectItem>();
            var folderProjects = new List<ProjectItem> { projectItem };
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
            project.ProjectItems.GetEnumerator().Returns(folderProjects.GetEnumerator());
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread
            var subProject = Substitute.For<Project>();
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
            projectItem.SubProject.Returns(subProject);
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread
            var envDteUtil = new EnvDteUtil(taskContext, dte2);
            var results = envDteUtil.GetSolutionProjects();
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(subProject, results[0]);
        }
    }
}

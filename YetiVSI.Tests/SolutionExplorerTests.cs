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
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.VCProjectEngine;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using YetiCommon.VSProject;

namespace YetiVSI.Test
{
    [TestFixture]
    class SolutionExplorerTests
    {
        VcProjectAdapter.Factory vcProjectAdapterFactory =
            Substitute.For<VcProjectAdapter.Factory>();
        IEnvDteUtil envDteUtil = Substitute.For<IEnvDteUtil>();
        SolutionExplorer.Factory solutionExplorerFactory;
        JoinableTaskContext taskContext;

        [SetUp]
        public void SetUp()
        {
            solutionExplorerFactory = new SolutionExplorer.Factory(vcProjectAdapterFactory);
            taskContext = new JoinableTaskContext();
        }

        [Test]
        public void EnumerateProjects()
        {
            var envDteProject = Substitute.For<Project>();
            envDteProject.Kind.Returns("test project kind");
            var envDteProjects = new List<Project> { envDteProject };
            envDteUtil.GetSolutionProjects().Returns(envDteProjects);
            var solutionExplorer = solutionExplorerFactory.Create(taskContext, envDteUtil);
            var project = Substitute.For<ISolutionExplorerProject>();
            var vcProject = Substitute.For<VCProject>();

            // Project.Object is dynamic. We need to cast it to object so NSubstitute can mock it.
            ((object)envDteProject.Object).Returns(vcProject);
            vcProjectAdapterFactory.Create(vcProject).Returns(project);

            var projects = solutionExplorer.EnumerateProjects();
            Assert.AreEqual(1, projects.Count());
            Assert.AreEqual(project, projects.First());
        }

        [Test]
        public void EnumerateProjectsNoProject()
        {
            var envDteProjects = new List<Project> { };
            envDteUtil.GetSolutionProjects().Returns(envDteProjects);
            var solutionExplorer = solutionExplorerFactory.Create(taskContext, envDteUtil);

            var projects = solutionExplorer.EnumerateProjects();
            Assert.AreEqual(0, projects.Count());
        }

        [Test]
        public void EnumerateProjectsNullCast()
        {
            var envDteProject = Substitute.For<Project>();
            envDteProject.Kind.Returns("test project kind");
            var envDteProjects = new List<Project> { envDteProject };
            envDteUtil.GetSolutionProjects().Returns(envDteProjects);
            var solutionExplorer = solutionExplorerFactory.Create(taskContext, envDteUtil);

            var projects = solutionExplorer.EnumerateProjects();
            Assert.AreEqual(0, projects.Count());
        }

        [Test]
        public void EnumerateProjectsFactoryCreateNull()
        {
            var envDteProject = Substitute.For<Project>();
            envDteProject.Kind.Returns("test project kind");
            var envDteProjects = new List<Project> { envDteProject };
            envDteUtil.GetSolutionProjects().Returns(envDteProjects);
            var solutionExplorer = solutionExplorerFactory.Create(taskContext, envDteUtil);
            var vcProject = Substitute.For<VCProject>();

            // Project.Object is dynamic. We need to cast it to object so NSubstitute can mock it.
            ((object)envDteProject.Object).Returns(vcProject);
            vcProjectAdapterFactory.Create(vcProject).Returns((ISolutionExplorerProject)null);

            var projects = solutionExplorer.EnumerateProjects();
            Assert.AreEqual(0, projects.Count());
        }
    }
}

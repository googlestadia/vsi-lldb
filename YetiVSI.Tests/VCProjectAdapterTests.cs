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
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using System.IO;
using System.Runtime.InteropServices;
using YetiVSI.ProjectSystem.Abstractions;

namespace YetiVSI.Test
{
    [TestFixture]
    class VCProjectAdapterTests
    {
        VcProjectAdapter.Factory vcProjectAdapterFactory = new VcProjectAdapter.Factory();

        [Test]
        public void GetOutputDirectory()
        {
            var projectPath = @"C:\GGP_project_path\";
            var projectValues = new ProjectValues
            {
                OutDir = @"C:\GGP_out_dir\",
            };

            var vcProject = CreateVcProject(projectValues, projectPath);
            ISolutionExplorerProject project = vcProjectAdapterFactory.Create(vcProject);
            Assert.AreEqual(RemoveTrailingSeparator(projectValues.OutDir), project.OutputDirectory);
        }

        [Test]
        public void GetOutputDirectoryEmpty()
        {
            var projectPath = @"C:\GGP_project_path\";
            var projectValues = new ProjectValues
            {
                OutDir = @"",
            };

            var vcProject = CreateVcProject(projectValues, projectPath);
            ISolutionExplorerProject project = vcProjectAdapterFactory.Create(vcProject);
            Assert.AreEqual(RemoveTrailingSeparator(projectPath), project.OutputDirectory);
        }

        [TestCase(@"C:\targetpath\", @"ggp_executable", @"C:\targetpath")]
        [TestCase(@"", @"ggp_executable", @"C:\GGP_project_path")]
        [TestCase(@"", @"..\ggp_executable", @"C:\")]
        [TestCase(@"", @"", @"C:\GGP_project_path")]
        [TestCase(null, null, @"C:\GGP_project_path")]
        public void GetTargetDirectoryMakefileProject(string targetPath,
            string targetFileName, string expected)
        {
            var projectPath = @"C:\GGP_project_path\";
            var projectValues = new ProjectValues
            {
                TargetPath = targetPath,
                TargetName = targetFileName,
            };

            var vcProject = CreateVcMakefileProject(projectValues, projectPath);
            ISolutionExplorerProject project = vcProjectAdapterFactory.Create(vcProject);
            Assert.AreEqual(expected, project.TargetDirectory);
        }

        [TestCase(@"C:\targetpath\", @"ggp_executable", @"C:\targetpath")]
        [TestCase(@"", @"ggp_executable", @"C:\output_project_path\output_path")]
        [TestCase(@"", @"..\ggp_executable", @"C:\output_project_path")]
        [TestCase(@"", @"", @"C:\output_project_path\output_path")]
        [TestCase(null, null, @"C:\output_project_path\output_path")]
        public void GetTargetDirectoryMsBuildProject(string targetPath, string targetFileName,
            string expected)
        {
            var projectPath = @"C:\GGP_project_path\";
            var outputPath = @"C:\output_project_path\output_path\";
            var projectValues = new ProjectValues
            {
                TargetPath = targetPath,
                TargetName = targetFileName,
                OutDir = outputPath,
            };

            var vcProject = CreateVcMsBuildProject(projectValues, projectPath);
            ISolutionExplorerProject project = vcProjectAdapterFactory.Create(vcProject);
            Assert.AreEqual(expected, project.TargetDirectory);
        }

        [TestCase(@"", @"ggp_executable", @"C:\GGP_project_path")]
        [TestCase(@"", @"..\ggp_executable", @"C:\")]
        [TestCase(@"C:\targetpath\", @"ggp_executable", @"C:\targetpath")]
        [TestCase(@"", @"", @"C:\GGP_project_path")]
        [TestCase(null, null, @"C:\GGP_project_path")]
        public void GetTargetDirectoryMsBuildProjectNoOutput(string targetPath,
            string targetFileName, string expected)
        {
            var projectPath = @"C:\GGP_project_path\";
            var projectValues = new ProjectValues
            {
                TargetPath = targetPath,
                TargetName = targetFileName,
            };

            var vcProject = CreateVcMsBuildProject(projectValues, projectPath);
            ISolutionExplorerProject project = vcProjectAdapterFactory.Create(vcProject);
            Assert.AreEqual(expected, project.TargetDirectory);
        }

        [Test]
        public void CreateVcProjectInvalidVcProject()
        {
            var projectPath = @"C:\Yeti_project_path\";
            var projectValues = new ProjectValues();
            var vcProject = CreateVcProject(projectValues, projectPath);
            vcProject.ActiveConfiguration.Rules.Item("ConfigurationGeneral")
                .Throws(new COMException(""));

            var project = vcProjectAdapterFactory.Create(vcProject);
            Assert.IsNull(project);
        }

        string RemoveTrailingSeparator(string path)
        {
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        class ProjectValues
        {
            public string TargetPath { get; set; } = "";
            public string OutDir { get; set; } = "";
            public string TargetName { get; set; } = "";
        }

        VCProject CreateVcMsBuildProject(ProjectValues projectValues, string projectPath)
        {
            var project = CreateVcProject(projectValues, projectPath);
            project.ActiveConfiguration.ConfigurationType.Returns(
                ConfigurationTypes.typeApplication);
            var generalRule = (IVCRulePropertyStorage)project.ActiveConfiguration.Rules
                .Item("ConfigurationGeneral");
            generalRule.GetEvaluatedPropertyValue("TargetName").Returns(projectValues.TargetName);
            return project;
        }

        VCProject CreateVcMakefileProject(ProjectValues projectValues, string projectPath)
        {
            var project = CreateVcProject(projectValues, projectPath);
            project.ActiveConfiguration.ConfigurationType.Returns(
                ConfigurationTypes.typeUnknown);
            var nmakeRule = Substitute.For<IVCRulePropertyStorage>();
            nmakeRule.GetEvaluatedPropertyValue("NMakeOutput").Returns(projectValues.TargetName);
            project.ActiveConfiguration.Rules.Item("ConfigurationNMake").Returns(nmakeRule);
            return project;
        }

        VCProject CreateVcProject(ProjectValues projectValues, string projectPath)
        {
            var configuration = Substitute.For<VCConfiguration>();
            var project = Substitute.For<VCProject>();
            project.ProjectDirectory.Returns(projectPath);
            project.ActiveConfiguration.Returns(configuration);
            var generalRule = Substitute.For<IVCRulePropertyStorage>();
            generalRule.GetEvaluatedPropertyValue("TargetPath").Returns(projectValues.TargetPath);
            generalRule.GetEvaluatedPropertyValue("OutDir").Returns(projectValues.OutDir);
            configuration.Rules.Item("ConfigurationGeneral").Returns(generalRule);
            return project;
        }
    }
}

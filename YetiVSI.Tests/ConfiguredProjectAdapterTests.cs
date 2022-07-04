// Copyright 2021 Google LLC
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

using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using NSubstitute;
using NUnit.Framework;
using YetiVSI.ProjectSystem.Abstractions;

namespace YetiVSI.Test
{
    [TestFixture]
    class ConfiguredProjectAdapterTests
    {
        static ConfiguredProjectAdapterTests()
        {
            if (!Microsoft.Build.Locator.MSBuildLocator.IsRegistered)
            {
                Microsoft.Build.Locator.MSBuildLocator.RegisterDefaults();
            }
        }

        ConfiguredProject CreateConfiguredProjectBase()
        {
            var services = Substitute.For<IConfiguredProjectServices>();
            var unconfiguredProject = Substitute.For<UnconfiguredProject>();
            var project = Substitute.For<ConfiguredProject>();

            project.Services.Returns(services);
            project.UnconfiguredProject.Returns(unconfiguredProject);

            return project;
        }

        [Test]
        public async Task GetOutputDirectoryAsync()
        {
            var projectPath = @"C:\GGP_project_path\";
            var projectValues = new ProjectValues
            {
                OutDir = @"C:\GGP_out_dir\",
            };

            var configuredProject = CreateConfiguredProject(projectValues, projectPath);
            IAsyncProject project = new ConfiguredProjectAdapter(configuredProject);
            Assert.AreEqual(RemoveTrailingSeparator(projectValues.OutDir),
                await project.GetOutputDirectoryAsync());
        }

        [Test]
        public async Task GetOutputDirectoryEmptyAsync()
        {
            var projectPath = @"C:\GGP_project_path\";
            var projectValues = new ProjectValues
            {
                OutDir = @"",
            };

            var configuredProject = CreateConfiguredProject(projectValues, projectPath);
            IAsyncProject project = new ConfiguredProjectAdapter(configuredProject);
            Assert.AreEqual(RemoveTrailingSeparator(projectValues.OutDir),
                await project.GetOutputDirectoryAsync());
        }

        [Test]
        public async Task GetTargetDirectoryAsync()
        {
            var projectPath = @"C:\GGP_project_path\";
            var targetFileName = @"GGP_executable.elf";
            var targetDirectory = @"C:\GGP\";
            var projectValues = new ProjectValues
            {
                TargetPath = Path.Combine(targetDirectory, targetFileName),
            };

            var configuredProject = CreateConfiguredProject(projectValues, projectPath);
            IAsyncProject project = new ConfiguredProjectAdapter(configuredProject);
            Assert.AreEqual(RemoveTrailingSeparator(targetDirectory),
                await project.GetTargetDirectoryAsync());
        }

        [Test]
        public async Task GetTargetDirectoryEmptyAsync()
        {
            var projectPath = @"C:\GGP_project_path\";
            var targetFileName = @"";
            var targetDirectory = @"";
            var projectValues = new ProjectValues
            {
                TargetPath = Path.Combine(targetDirectory, targetFileName),
            };

            var configuredProject = CreateConfiguredProject(projectValues, projectPath);
            IAsyncProject project = new ConfiguredProjectAdapter(configuredProject);
            Assert.AreEqual(RemoveTrailingSeparator(targetDirectory),
                await project.GetTargetDirectoryAsync());
        }

        [Test]
        public async Task GetTargetFileNameAsync()
        {
            var projectPath = @"C:\GGP_project_path\";
            var targetFileName = @"GGP_executable.elf";
            var targetDirectory = @"C:\GGP\";
            var projectValues = new ProjectValues
            {
                TargetPath = Path.Combine(targetDirectory, targetFileName),
            };

            var configuredProject = CreateConfiguredProject(projectValues, projectPath);
            IAsyncProject project = new ConfiguredProjectAdapter(configuredProject);
            Assert.AreEqual(targetFileName, await project.GetTargetFileNameAsync());
        }

        [Test]
        public async Task GetTargetFilenameEmptyAsync()
        {
            var projectPath = @"C:\GGP_project_path\";
            var targetFileName = @"";
            var targetDirectory = @"C:\GGP\";
            var projectValues = new ProjectValues
            {
                TargetPath = Path.Combine(targetDirectory, targetFileName),
            };

            var configuredProject = CreateConfiguredProject(projectValues, projectPath);
            IAsyncProject project = new ConfiguredProjectAdapter(configuredProject);
            Assert.AreEqual(targetFileName, await project.GetTargetFileNameAsync());
        }

        [Test]
        public async Task GetGgpGameletLaunchArgumentsAsync()
        {
            var projectPath = @"C:\GGP_project_path\";
            var projectValues = new ProjectValues
            {
                GgpGameletLaunchArguments = "GGP Launch Arguments",
            };
            var configuredProject = CreateConfiguredProject(projectValues, projectPath);
            IAsyncProject project = new ConfiguredProjectAdapter(configuredProject);
            Assert.AreEqual(projectValues.GgpGameletLaunchArguments,
                await project.GetGameletLaunchArgumentsAsync());
        }

        [Test]
        public async Task GetGgpGameletEnvironmentVariablesAsync()
        {
            var projectPath = @"C:\GGP_project_path\";
            var projectValues = new ProjectValues
            {
                GgpGameletEnvironmentVariables = "GGP Env Vars",
            };
            var configuredProject = CreateConfiguredProject(projectValues, projectPath);
            IAsyncProject project = new ConfiguredProjectAdapter(configuredProject);
            Assert.AreEqual(projectValues.GgpGameletEnvironmentVariables,
                await project.GetGameletEnvironmentVariablesAsync());
        }

        [Test]
        public async Task GetGgpApplicationAsync()
        {
            var projectPath = @"C:\GGP_project_path\";
            var projectValues = new ProjectValues
            {
                GgpApplication = "GGP App Name",
            };
            var configuredProject = CreateConfiguredProject(projectValues, projectPath);
            IAsyncProject project = new ConfiguredProjectAdapter(configuredProject);
            Assert.AreEqual(projectValues.GgpApplication, await project.GetApplicationAsync());
        }

        [Test]
        public async Task GetGgpCustomDeployOnLaunchAsync()
        {
            var projectPath = @"C:\GGP_project_path\";
            var projectValues = new ProjectValues
            {
                GgpCustomDeployOnLaunch = "GGP Launch Command",
            };
            var configuredProject = CreateConfiguredProject(projectValues, projectPath);
            IAsyncProject project = new ConfiguredProjectAdapter(configuredProject);
            Assert.AreEqual(projectValues.GgpCustomDeployOnLaunch,
                await project.GetCustomDeployOnLaunchAsync());
        }

        [TestCase("delta", DeployOnLaunchSetting.DELTA)]
        [TestCase("false", DeployOnLaunchSetting.FALSE)]
        [TestCase("always", DeployOnLaunchSetting.ALWAYS)]
        public async Task GetGgpDeployOnLaunchAsync(string stringValue,
                                                    DeployOnLaunchSetting enumValue)
        {
            var projectPath = @"C:\GGP_project_path\";
            var projectValues = new ProjectValues
            {
                GgpDeployOnLaunch = stringValue,
            };

            var configuredProject = CreateConfiguredProject(projectValues, projectPath);
            IAsyncProject project = new ConfiguredProjectAdapter(configuredProject);
            Assert.AreEqual(enumValue, await project.GetDeployOnLaunchAsync());
        }

        [TestCase(GgpSurfaceEnforcementMode.Off)]
        [TestCase(GgpSurfaceEnforcementMode.Warn)]
        [TestCase(GgpSurfaceEnforcementMode.Block)]
        public async Task GetGgpSurfaceEnforcementModeAsync(
            GgpSurfaceEnforcementMode surfaceEnforcement)
        {
            var projectPath = string.Empty;
            var projectValues = new ProjectValues
            {
                GgpSurfaceEnforcementMode = surfaceEnforcement.ToString()
            };

            var configuredProject = CreateConfiguredProject(projectValues, projectPath);
            IAsyncProject project = new ConfiguredProjectAdapter(configuredProject);
            Assert.AreEqual(surfaceEnforcement, await project.GetSurfaceEnforcementAsync());
        }

        [Test]
        public async Task GetGgpLaunchRenderDocAsync()
        {
            var projectPath = @"C:\GGP_project_path\";
            var projectValues = new ProjectValues
            {
                GgpLaunchRenderDoc = "true",
            };

            var configuredProject = CreateConfiguredProject(projectValues, projectPath);
            IAsyncProject project = new ConfiguredProjectAdapter(configuredProject);
            Assert.True(await project.GetLaunchRenderDocAsync());
        }

        [Test]
        public async Task AbsoluteRootPathAsync()
        {
            var projectPath = @"C:\GGP_project_path\";
            var projectValues = new ProjectValues();

            var configuredProject = CreateConfiguredProject(projectValues, projectPath);
            IAsyncProject project = new ConfiguredProjectAdapter(configuredProject);
            Assert.AreEqual(RemoveTrailingSeparator(projectPath),
                await project.GetAbsoluteRootPathAsync());
        }

        [Test]
        public async Task GetTargetPathAsync()
        {
            var projectPath = @"C:\GGP_project_path\";
            var targetFileName = @"GGP_executable.elf";
            var targetDirectory = @"C:\GGP\";
            var projectValues = new ProjectValues
            {
                TargetPath = Path.Combine(targetDirectory, targetFileName),
            };

            var configuredProject = CreateConfiguredProject(projectValues, projectPath);
            IAsyncProject project = new ConfiguredProjectAdapter(configuredProject);
            Assert.AreEqual(RemoveTrailingSeparator(projectValues.TargetPath),
                await project.GetTargetPathAsync());
        }

        [Test]
        public async Task GetTargetPathEmptyAsync()
        {
            var projectPath = @"C:\GGP_project_path\";
            var targetFileName = @"";
            var targetDirectory = @"";
            var projectValues = new ProjectValues
            {
                TargetPath = Path.Combine(targetDirectory, targetFileName),
            };

            var configuredProject = CreateConfiguredProject(projectValues, projectPath);
            IAsyncProject project = new ConfiguredProjectAdapter(configuredProject);
            Assert.AreEqual(RemoveTrailingSeparator(projectValues.TargetPath),
                await project.GetTargetPathAsync());
        }

        [Test]
        public async Task GetGgpTestAccountAsync()
        {
            string projectPath = @"C:\GGP_project_path\";
            var projectValues = new ProjectValues
            {
                GgpTestAccount = "GGP Test Account",
            };

            var configuredProject = CreateConfiguredProject(projectValues, projectPath);
            IAsyncProject project = new ConfiguredProjectAdapter(configuredProject);
            Assert.AreEqual(projectValues.GgpTestAccount, await project.GetTestAccountAsync());
        }

        [Test]
        public async Task GetGgpExternalIdAsync()
        {
            string projectPath = @"C:\GGP_project_path\";
            var projectValues = new ProjectValues
            {
                GgpTestAccount = "GGP External ID",
            };

            var configuredProject = CreateConfiguredProject(projectValues, projectPath);
            IAsyncProject project = new ConfiguredProjectAdapter(configuredProject);
            Assert.AreEqual(projectValues.GgpExternalId, await project.GetExternalIdAsync());
        }

        [Test]
        public async Task GetGgpEndpointAsync()
        {
            const string projectPath = @"C:\GGP_project_path\";
            var projectValues = new ProjectValues
            {
                GgpEndpoint = "Test Client",
            };

            var configuredProject = CreateConfiguredProject(projectValues, projectPath);
            IAsyncProject project = new ConfiguredProjectAdapter(configuredProject);
            Assert.AreEqual(StadiaEndpoint.TestClient, await project.GetEndpointAsync());
        }

        [Test]
        public async Task GetExecutablePathAsync()
        {
            var projectPath = @"C:\GGP_project_path\";
            var projectValues = new ProjectValues
            {
                ExecutablePath = @"C:\GGP_exe_path\",
            };

            var configuredProject = CreateConfiguredProject(projectValues, projectPath);
            IAsyncProject project = new ConfiguredProjectAdapter(configuredProject);
            Assert.AreEqual(RemoveTrailingSeparator(projectValues.ExecutablePath),
                await project.GetExecutablePathAsync());
        }

        [Test]
        public async Task GetGgpLaunchRgpAsync()
        {
            var projectPath = @"C:\GGP_project_path\";
            var projectValues = new ProjectValues
            {
                GgpLaunchRgp = "true",
            };

            var configuredProject = CreateConfiguredProject(projectValues, projectPath);
            IAsyncProject project = new ConfiguredProjectAdapter(configuredProject);
            Assert.True(await project.GetLaunchRgpAsync());
        }

        [Test]
        public async Task GetGgpLaunchDiveAsync()
        {
            var projectPath = @"C:\GGP_project_path\";
            var projectValues = new ProjectValues
            {
                GgpLaunchDive = "true",
            };

            var configuredProject = CreateConfiguredProject(projectValues, projectPath);
            IAsyncProject project = new ConfiguredProjectAdapter(configuredProject);
            Assert.True(await project.GetLaunchDiveAsync());
        }

        [Test]
        public async Task GetGgpLaunchOrbitAsync()
        {
            var projectPath = @"C:\GGP_project_path\";
            var projectValues = new ProjectValues
            {
                GgpLaunchOrbit = "true",
            };

            var configuredProject = CreateConfiguredProject(projectValues, projectPath);
            IAsyncProject project = new ConfiguredProjectAdapter(configuredProject);
            Assert.True(await project.GetLaunchOrbitAsync());
        }

        [Test]
        public async Task GetGgpVulkanDriverVariantAsync()
        {
            var projectPath = @"C:\GGP_project_path\";
            var projectValues = new ProjectValues
            {
                GgpVulkanDriverVariant = "optprintasserts",
            };

            var configuredProject = CreateConfiguredProject(projectValues, projectPath);
            IAsyncProject project = new ConfiguredProjectAdapter(configuredProject);
            Assert.AreEqual(projectValues.GgpVulkanDriverVariant,
                await project.GetVulkanDriverVariantAsync());
        }

        [Test]
        public async Task GetQueryParamsAsync()
        {
            var projectPath = @"C:\Yeti_project_path\";
            var projectValues = new ProjectValues
            {
                GgpQueryParams = "test1=5&test2=10"
            };
            var configuredProject = CreateConfiguredProject(projectValues, projectPath);

            var project = new ConfiguredProjectAdapter(configuredProject);
            Assert.AreEqual(await project.GetQueryParamsAsync(), projectValues.GgpQueryParams);
        }

        [Test]
        public async Task CreateConfiguredProjectInvalidDeployOnLaunchAsync()
        {
            var projectPath = @"C:\Yeti_project_path\";
            var projectValues = new ProjectValues
            {
                GgpDeployOnLaunch = "invalid_bool",
            };
            var configuredProject = CreateConfiguredProject(projectValues, projectPath);

            var project = new ConfiguredProjectAdapter(configuredProject);
            Assert.AreEqual(DeployOnLaunchSetting.DELTA, await project.GetDeployOnLaunchAsync());
        }

        [Test]
        public async Task CreateConfiguredProjectInvalidInstanceSurfaceEnforcementAsync()
        {
            var projectPath = string.Empty;
            var projectValues = new ProjectValues
            {
                GgpSurfaceEnforcementMode = "invalid_setting",
            };
            var configuredProject = CreateConfiguredProject(projectValues, projectPath);

            var project = new ConfiguredProjectAdapter(configuredProject);
            Assert.AreEqual(
                GgpSurfaceEnforcementMode.Off,
                await project.GetSurfaceEnforcementAsync());
        }

        [Test]
        public async Task CreateConfiguredProjectInvalidLaunchRenderDocAsync()
        {
            var projectPath = @"C:\Yeti_project_path\";
            var projectValues = new ProjectValues
            {
                GgpLaunchRenderDoc = "invalid_bool",
            };
            var configuredProject = CreateConfiguredProject(projectValues, projectPath);

            var project = new ConfiguredProjectAdapter(configuredProject);
            Assert.False(await project.GetLaunchRenderDocAsync());
        }

        [Test]
        public async Task CreateConfiguredProjectInvalidLaunchRgpAsync()
        {
            var projectPath = @"C:\Yeti_project_path\";
            var projectValues = new ProjectValues
            {
                GgpLaunchRgp = "invalid_bool",
            };
            var configuredProject = CreateConfiguredProject(projectValues, projectPath);

            var project = new ConfiguredProjectAdapter(configuredProject);
            Assert.False(await project.GetLaunchRgpAsync());
        }

        [Test]
        public async Task CreateConfiguredProjectInvalidLaunchDiveAsync()
        {
            var projectPath = @"C:\Yeti_project_path\";
            var projectValues = new ProjectValues
            {
                GgpLaunchDive = "invalid_bool",
            };
            var configuredProject = CreateConfiguredProject(projectValues, projectPath);

            var project = new ConfiguredProjectAdapter(configuredProject);
            Assert.False(await project.GetLaunchDiveAsync());
        }

        [Test]
        public async Task CreateConfiguredProjectInvalidLaunchOrbitAsync()
        {
            var projectPath = @"C:\Yeti_project_path\";
            var projectValues = new ProjectValues
            {
                GgpLaunchOrbit = "invalid_bool",
            };
            var configuredProject = CreateConfiguredProject(projectValues, projectPath);

            var project = new ConfiguredProjectAdapter(configuredProject);
            Assert.False(await project.GetLaunchOrbitAsync());
        }

        class ProjectValues
        {
            public string GgpGameletLaunchArguments { get; set; } = "";
            public string GgpGameletEnvironmentVariables { get; set; } = "";
            public string GgpApplication { get; set; } = "";
            public string GgpDeployOnLaunch { get; set; } = "";
            public string GgpSurfaceEnforcementMode { get; set; } = "";
            public string GgpCustomDeployOnLaunch { get; set; } = "";
            public string GgpLaunchRenderDoc { get; set; } = "";
            public string GgpLaunchRgp { get; set; } = "";
            public string GgpLaunchDive { get; set; } = "";
            public string GgpLaunchOrbit { get; set; } = "";
            public string GgpVulkanDriverVariant { get; set; } = "";
            public string GgpTestAccount { get; set; } = "";
            public string GgpExternalId { get; set; } = "";
            public string GgpEndpoint { get; set; } = "";
            public string TargetPath { get; set; } = "";
            public string OutDir { get; set; } = "";
            public string ExecutablePath { get; set; } = "";
            public string TargetName { get; set; } = "";
            public string GgpQueryParams { get; set; } = "";
        }

        string RemoveTrailingSeparator(string path)
        {
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        ConfiguredProject CreateConfiguredProject(ProjectValues projectValues,
            string projectPath)
        {
            var project = CreateConfiguredProjectBase();

            var userProperties = Substitute.For<IProjectProperties>();
            var userPropertiesProvider = Substitute.For<IProjectPropertiesProvider>();
            var projectProperties = Substitute.For<IProjectProperties>();
            var projectPropertiesProvider = Substitute.For<IProjectPropertiesProvider>();

            project.UnconfiguredProject.FullPath.Returns(projectPath);
            project.Services.UserPropertiesProvider.Returns(userPropertiesProvider);
            userPropertiesProvider.GetCommonProperties().Returns(userProperties);
            project.Services.ProjectPropertiesProvider.Returns(projectPropertiesProvider);
            projectPropertiesProvider.GetCommonProperties().Returns(projectProperties);

            userProperties.GetEvaluatedPropertyValueAsync("GgpGameletLaunchArguments")
                .Returns(projectValues.GgpGameletLaunchArguments);
            userProperties.GetEvaluatedPropertyValueAsync("GgpGameletEnvironmentVariables")
                .Returns(projectValues.GgpGameletEnvironmentVariables);
            userProperties.GetEvaluatedPropertyValueAsync("GgpApplication")
                .Returns(projectValues.GgpApplication);
            userProperties.GetEvaluatedPropertyValueAsync("GgpDeployOnLaunch")
                .Returns(projectValues.GgpDeployOnLaunch);
            userProperties.GetEvaluatedPropertyValueAsync("GgpSurfaceEnforcementMode")
                .Returns(projectValues.GgpSurfaceEnforcementMode);
            userProperties.GetEvaluatedPropertyValueAsync("GgpCustomDeployOnLaunch")
                .Returns(projectValues.GgpCustomDeployOnLaunch);
            userProperties.GetEvaluatedPropertyValueAsync("GgpLaunchRenderDoc")
                .Returns(projectValues.GgpLaunchRenderDoc);
            userProperties.GetEvaluatedPropertyValueAsync("GgpLaunchRgp")
                .Returns(projectValues.GgpLaunchRgp);
            userProperties.GetEvaluatedPropertyValueAsync("GgpLaunchDive")
                .Returns(projectValues.GgpLaunchDive);
            userProperties.GetEvaluatedPropertyValueAsync("GgpLaunchOrbit")
                .Returns(projectValues.GgpLaunchOrbit);
            userProperties.GetEvaluatedPropertyValueAsync("GgpVulkanDriverVariant")
                .Returns(projectValues.GgpVulkanDriverVariant);
            userProperties.GetEvaluatedPropertyValueAsync("GgpTestAccount")
                .Returns(projectValues.GgpTestAccount);
            userProperties.GetEvaluatedPropertyValueAsync("GgpExternalId")
                .Returns(projectValues.GgpExternalId);

            userProperties.GetEvaluatedPropertyValueAsync("TargetPath")
                .Returns(projectValues.TargetPath);
            userProperties.GetEvaluatedPropertyValueAsync("OutDir").Returns(projectValues.OutDir);
            userProperties.GetEvaluatedPropertyValueAsync("GgpQueryParams")
                .Returns(projectValues.GgpQueryParams);
            projectProperties.GetEvaluatedPropertyValueAsync("ExecutablePath")
                .Returns(projectValues.ExecutablePath);

            return project;
        }
    }
}

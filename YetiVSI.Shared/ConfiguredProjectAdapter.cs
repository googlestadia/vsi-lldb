﻿// Copyright 2021 Google LLC
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
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using YetiVSI.ProjectSystem.Abstractions;

namespace YetiVSI
{
    public class ConfiguredProjectAdapter : IAsyncProject
    {
        readonly UnconfiguredProject unconfiguredProject;
        readonly IProjectProperties projectProperties;
        readonly IProjectProperties userProperties;

        public ConfiguredProjectAdapter(ConfiguredProject configuredProject)
        {
            // UnconfiguredProject is the same in VS2017 and VS2019.
            unconfiguredProject = configuredProject.UnconfiguredProject;

#if VS2019
            // Services is an interface in VS2017 and an abstract class in VS2019.
            // Use reflection to bypass this difference, the rest of the fields are the same.
            var servicesProperty = configuredProject.GetType().GetProperty("Services");
            var services = servicesProperty.GetValue(configuredProject, null);

            // Get to UserPropertiesProvider.GetCommonProperties().
            var uppProperty = services.GetType().GetProperty("UserPropertiesProvider");
            var upp = (IProjectPropertiesProvider)uppProperty.GetValue(services);
            userProperties = upp.GetCommonProperties();

            // Get to ProjectPropertiesProvider.GetCommonProperties().
            var pppProperty = services.GetType().GetProperty("ProjectPropertiesProvider");
            var ppp = (IProjectPropertiesProvider)pppProperty.GetValue(services);
            projectProperties = ppp.GetCommonProperties();

#elif VS2022
            userProperties =
                configuredProject.Services.UserPropertiesProvider.GetCommonProperties();
            projectProperties =
                configuredProject.Services.ProjectPropertiesProvider.GetCommonProperties();

#else
#error Unsupported Visual Studio version
#endif
        }

        public Task<string> GetAbsoluteRootPathAsync()
        {
            return Task.FromResult(unconfiguredProject.MakeRooted(".").TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        public async Task<string> GetApplicationAsync()
        {
            var application = await userProperties.GetEvaluatedPropertyValueAsync(
                ProjectPropertyName.GgpApplication);
            return application;
        }

        public async Task<string> GetCustomDeployOnLaunchAsync()
        {
            var customDeployOnLaunch = await userProperties.GetEvaluatedPropertyValueAsync(
                ProjectPropertyName.GgpCustomDeployOnLaunch);
            return customDeployOnLaunch;
        }

        public async Task<DeployOnLaunchSetting> GetDeployOnLaunchAsync()
        {
            var deployOnLaunchString = await userProperties.GetEvaluatedPropertyValueAsync(
                ProjectPropertyName.GgpDeployOnLaunch);
            if (Enum.TryParse(deployOnLaunchString, true, out DeployOnLaunchSetting deployOnLaunch))
            {
                return deployOnLaunch;
            }

            return DeployOnLaunchSetting.DELTA;
        }

        public bool GetDeployOrbitVulkanLayerOnLaunch()
        {
            return true;
        }

        public async Task<GgpSurfaceEnforcementMode> GetSurfaceEnforcementAsync()
        {
            string surfaceEnforcementString = await userProperties.GetEvaluatedPropertyValueAsync(
                ProjectPropertyName.GgpSurfaceEnforcementMode);

            if (!Enum.TryParse(surfaceEnforcementString, true,
                               out GgpSurfaceEnforcementMode surfaceEnforcement))
            {
                return GgpSurfaceEnforcementMode.Off;
            }
            return surfaceEnforcement;
        }

        public async Task<string> GetExecutablePathAsync()
        {
            var executablePath = await projectProperties.GetEvaluatedPropertyValueAsync(
                ProjectPropertyName.ExecutablePath);
            return executablePath.TrimEnd(Path.DirectorySeparatorChar,
                                          Path.AltDirectorySeparatorChar);
        }

        public async Task<string> GetGameletEnvironmentVariablesAsync()
        {
            var gameletEnvironmentVariables = await userProperties.GetEvaluatedPropertyValueAsync(
                ProjectPropertyName.GgpGameletEnvironmentVariables);
            return gameletEnvironmentVariables;
        }

        public async Task<string> GetGameletLaunchExecutableAsync()
        {
            var gameletLaunchExecutable = await userProperties.GetEvaluatedPropertyValueAsync(
                ProjectPropertyName.GgpGameletLaunchExecutable);
            if (string.IsNullOrWhiteSpace(gameletLaunchExecutable))
            {
                return await GetTargetFileNameAsync();
            }
            return gameletLaunchExecutable;
        }

        public async Task<string> GetGameletLaunchArgumentsAsync()
        {
            var gameletLaunchArguments = await userProperties.GetEvaluatedPropertyValueAsync(
                ProjectPropertyName.GgpGameletLaunchArguments);
            return gameletLaunchArguments;
        }

        public async Task<bool> GetLaunchRenderDocAsync()
        {
            var launchRenderDocString = await userProperties.GetEvaluatedPropertyValueAsync(
                ProjectPropertyName.GgpLaunchRenderDoc);
            bool.TryParse(launchRenderDocString, out bool launchRenderDoc);
            return launchRenderDoc;
        }

        public async Task<bool> GetLaunchRgpAsync()
        {
            var launchRgpString = await userProperties.GetEvaluatedPropertyValueAsync(
                ProjectPropertyName.GgpLaunchRgp);
            bool.TryParse(launchRgpString, out bool launchRgp);
            return launchRgp;
        }

        public async Task<bool> GetLaunchDiveAsync()
        {
            var launchDiveString = await userProperties.GetEvaluatedPropertyValueAsync(
                ProjectPropertyName.GgpLaunchDive);
            bool.TryParse(launchDiveString, out bool launchDive);
            return launchDive;
        }

        public async Task<bool> GetLaunchOrbitAsync()
        {
            var launchOrbitString = await userProperties.GetEvaluatedPropertyValueAsync(
                ProjectPropertyName.GgpLaunchOrbit);
            bool.TryParse(launchOrbitString, out bool launchOrbit);
            return launchOrbit;
        }

        public async Task<string> GetVulkanDriverVariantAsync()
        {
            var vulkanDriverVariant = await userProperties.GetEvaluatedPropertyValueAsync(
                ProjectPropertyName.GgpVulkanDriverVariant);
            return vulkanDriverVariant;
        }

        public async Task<string> GetTargetFileNameAsync()
        {
            var targetPath =
                await userProperties.GetEvaluatedPropertyValueAsync(ProjectPropertyName.TargetPath);
            if (!string.IsNullOrEmpty(targetPath))
            {
                return Path.GetFileName(Path.GetFullPath(targetPath));
            }
            return "";
        }

        public async Task<string> GetTargetPathAsync()
        {
            var targetPath =
                await userProperties.GetEvaluatedPropertyValueAsync(ProjectPropertyName.TargetPath);
            if (!string.IsNullOrEmpty(targetPath))
            {
                return Path.GetFullPath(targetPath);
            }
            return "";
        }

        public async Task<string> GetTestAccountAsync()
        {
            string testAccount =
                await userProperties.GetEvaluatedPropertyValueAsync(
                    ProjectPropertyName.GgpTestAccount);
            return testAccount;
        }

        public async Task<string> GetExternalIdAsync()
        {
            string externalId =
                await userProperties.GetEvaluatedPropertyValueAsync(
                    ProjectPropertyName.GgpExternalId);
            return externalId;
        }

        public async Task<StadiaEndpoint> GetEndpointAsync()
        {
            string endpointString =
                await userProperties.GetEvaluatedPropertyValueAsync(ProjectPropertyName.GgpEndpoint);
            return Enum.TryParse(endpointString, true, out StadiaEndpoint endpoint)
                       ? endpoint
                       : StadiaEndpoint.TestClient;
        }

        public async Task<string> GetQueryParamsAsync()
        {
            var queryParams = await userProperties.GetEvaluatedPropertyValueAsync(
                ProjectPropertyName.GgpQueryParams);
            return queryParams;
        }

        public Task<string> GetDeployExecutableOnLaunchRawAsync() =>
            userProperties.GetEvaluatedPropertyValueAsync(ProjectPropertyName.GgpDeployOnLaunch);

        public Task<string> GetSurfaceEnforcementModeRawAsync() =>
            userProperties.GetEvaluatedPropertyValueAsync(
                ProjectPropertyName.GgpSurfaceEnforcementMode);

        public Task<string> GetLaunchWithRenderDocRawAsync() =>
            userProperties.GetEvaluatedPropertyValueAsync(ProjectPropertyName.GgpLaunchRenderDoc);

        public Task<string> GetLaunchWithRgpRawAsync() =>
            userProperties.GetEvaluatedPropertyValueAsync(ProjectPropertyName.GgpLaunchRgp);

        public Task<string> GetVulkanDriverVariantRawAsync() =>
            userProperties.GetEvaluatedPropertyValueAsync(
                ProjectPropertyName.GgpVulkanDriverVariant);

        public Task<string> GetStadiaEndpointRawAsync() =>
            userProperties.GetEvaluatedPropertyValueAsync(ProjectPropertyName.GgpEndpoint);

        public async Task<string> GetOutputDirectoryAsync()
        {
            var outDir = await userProperties.GetEvaluatedPropertyValueAsync(
                ProjectPropertyName.OutputDirectory);
            if (!string.IsNullOrEmpty(outDir))
            {
                return Path.GetFullPath(outDir).TrimEnd(Path.DirectorySeparatorChar,
                                                        Path.AltDirectorySeparatorChar);
            }
            return "";
        }

        public async Task<string> GetTargetDirectoryAsync()
        {
            var targetPath =
                await userProperties.GetEvaluatedPropertyValueAsync(ProjectPropertyName.TargetPath);
            if (!string.IsNullOrEmpty(targetPath))
            {
                return Path.GetDirectoryName(Path.GetFullPath(targetPath));
            }
            return "";
        }
    }
}

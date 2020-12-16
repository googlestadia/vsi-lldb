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

using GgpGrpc.Models;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using System;
using System.IO;
using System.Threading.Tasks;
using YetiCommon;
using YetiCommon.VSProject;

namespace YetiVSI
{
    public class ConfiguredProjectAdapter : IAsyncProject
    {
        readonly ConfiguredProject configuredProject;
        readonly IProjectProperties projectProperties;
        readonly IProjectProperties userProperties;

        public ConfiguredProjectAdapter(ConfiguredProject configuredProject)
        {
            this.configuredProject = configuredProject;
            userProperties =
                configuredProject.Services.UserPropertiesProvider.GetCommonProperties();
            projectProperties =
                configuredProject.Services.ProjectPropertiesProvider.GetCommonProperties();
        }

        public Task<string> GetAbsoluteRootPathAsync()
        {
            return Task.FromResult(FileUtil.RemoveTrailingSeparator(
                configuredProject.UnconfiguredProject.MakeRooted(".")));
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
            DeployOnLaunchSetting deployOnLaunch;
            if (Enum.TryParse(deployOnLaunchString, true, out deployOnLaunch))
            {
                return deployOnLaunch;
            }
            else
            {
                return DeployOnLaunchSetting.TRUE;
            }
        }

        public async Task<DeployCompressionSetting> GetDeployCompressionAsync()
        {
            string deployCompressionString = await userProperties.GetEvaluatedPropertyValueAsync(
                ProjectPropertyName.GgpDeployCompression);
            DeployCompressionSetting deployCompression;
            if (!Enum.TryParse(deployCompressionString, true, out deployCompression))
            {
                return DeployCompressionSetting.Compressed;
            }
            return deployCompression;
        }

        public async Task<SurfaceEnforcementSetting> GetSurfaceEnforcementAsync()
        {
            string surfaceEnforcementString = await userProperties.GetEvaluatedPropertyValueAsync(
                ProjectPropertyName.GgpSurfaceEnforcementMode);

            if (!Enum.TryParse(surfaceEnforcementString, true,
                               out SurfaceEnforcementSetting surfaceEnforcement))
            {
                return SurfaceEnforcementSetting.Off;
            }
            return surfaceEnforcement;
        }

        public async Task<string> GetExecutablePathAsync()
        {
            var executablePath = await projectProperties.GetEvaluatedPropertyValueAsync(
                ProjectPropertyName.ExecutablePath);
            return FileUtil.RemoveTrailingSeparator(executablePath);
        }

        public async Task<string> GetGameletEnvironmentVariablesAsync()
        {
            var gameletEnvironmentVariables = await userProperties.GetEvaluatedPropertyValueAsync(
                ProjectPropertyName.GgpGameletEnvironmentVariables);
            return gameletEnvironmentVariables;
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
            bool launchRenderDoc;
            bool.TryParse(launchRenderDocString, out launchRenderDoc);
            return launchRenderDoc;
        }

        public async Task<bool> GetLaunchRgpAsync()
        {
            var launchRgpString = await userProperties.GetEvaluatedPropertyValueAsync(
                ProjectPropertyName.GgpLaunchRgp);
            bool launchRgp;
            bool.TryParse(launchRgpString, out launchRgp);
            return launchRgp;
        }

        public async Task<string> GetVulkanDriverVariantAsync()
        {
            var vulkanDriverVariant = await userProperties.GetEvaluatedPropertyValueAsync(
                ProjectPropertyName.GgpVulkanDriverVariant);
            return vulkanDriverVariant;
        }

        public async Task<string> GetTargetFileNameAsync()
        {
            var targetPath = await userProperties.GetEvaluatedPropertyValueAsync(
                ProjectPropertyName.TargetPath);
            if (!string.IsNullOrEmpty(targetPath))
            {
                return Path.GetFileName(Path.GetFullPath(targetPath));
            }
            return "";
        }

        public async Task<string> GetTargetPathAsync()
        {
            var targetPath = await userProperties.GetEvaluatedPropertyValueAsync(
                ProjectPropertyName.TargetPath);
            if (!string.IsNullOrEmpty(targetPath))
            {
                return Path.GetFullPath(targetPath);
            }
            return "";
        }

        public async Task<string> GetTestAccountAsync()
        {
            var testAccount = await userProperties.GetEvaluatedPropertyValueAsync(
                ProjectPropertyName.GgpTestAccount);
            return testAccount;
        }

        public async Task<string> GetQueryParamsAsync()
        {
            var queryParams = await userProperties.GetEvaluatedPropertyValueAsync(
                ProjectPropertyName.GgpQueryParams);
            return queryParams;
        }

        public async Task<string> GetOutputDirectoryAsync()
        {
            var outDir = await userProperties.GetEvaluatedPropertyValueAsync(
                ProjectPropertyName.OutputDirectory);
            if (!string.IsNullOrEmpty(outDir))
            {
                return FileUtil.RemoveTrailingSeparator(Path.GetFullPath(outDir));
            }
            return "";
        }

        public async Task<string> GetTargetDirectoryAsync()
        {
            var targetPath = await userProperties.GetEvaluatedPropertyValueAsync(
                ProjectPropertyName.TargetPath);
            if (!string.IsNullOrEmpty(targetPath))
            {
                return Path.GetDirectoryName(Path.GetFullPath(targetPath));
            }
            return "";
        }
    }
}

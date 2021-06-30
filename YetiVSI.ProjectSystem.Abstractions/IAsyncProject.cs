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

// This file is defined in the YetiCommon assembly as a workaround while it needs to be referenced
// by both YetiVSI and VSFake. It should be moved back to YetiVSI when possible (see (internal)).
using GgpGrpc.Models;
using System.Threading.Tasks;

namespace YetiVSI.ProjectSystem.Abstractions
{
    /// <summary>
    /// Specifies under what conditions to deploy executable on launch.
    /// </summary>
    public enum DeployOnLaunchSetting
    {
        /// <summary>
        /// Deploy the executable delta.
        /// </summary>
        DELTA,
        /// <summary>
        /// Never deploy the executable.
        /// </summary>
        FALSE,
        /// <summary>
        /// Always deploy the executable.
        /// </summary>
        ALWAYS
    }

    /// <summary>
    /// Specifies endpoint to be used on launch.
    /// </summary>
    public enum StadiaEndpoint
    {
        /// <summary>
        /// Test Client.
        /// </summary>
        TestClient,
        /// <summary>
        /// Player endpoint.
        /// </summary>
        PlayerEndpoint,
        /// <summary>
        /// The launch can be picked up on any endpoint.
        /// </summary>
        AnyEndpoint
    }

    /// <summary>
    /// Describes a Stadia Visual Studio project.
    /// </summary>
    public interface IAsyncProject
    {
        /// <summary>
        /// Get the full path to the project's directory.
        /// </summary>
        Task<string> GetAbsoluteRootPathAsync();

        /// <summary>
        /// Get user-specified project output directory.
        /// </summary>
        Task<string> GetOutputDirectoryAsync();

        /// <summary>
        /// Get full local path to the target executable's directory.
        /// </summary>
        Task<string> GetTargetDirectoryAsync();

        /// <summary>
        /// Get path to the Yeti SDK tools bin directory.
        /// </summary>
        Task<string> GetExecutablePathAsync();

        /// <summary>
        /// Get arguments to pass to the game binary.
        /// </summary>
        Task<string> GetGameletLaunchArgumentsAsync();

        /// <summary>
        /// Get environment variables to set when launching the game binary.
        /// </summary>
        Task<string> GetGameletEnvironmentVariablesAsync();

        /// <summary>
        /// Get full local path to the target executable.
        /// </summary>
        Task<string> GetTargetPathAsync();

        /// <summary>
        /// Get file name of the target executable.
        /// </summary>
        Task<string> GetTargetFileNameAsync();

        /// <summary>
        /// Get user-specified project ID or Name.
        /// </summary>
        Task<string> GetApplicationAsync();

        /// <summary>
        /// Get true when the exe should be deployed on launch.
        /// </summary>
        Task<DeployOnLaunchSetting> GetDeployOnLaunchAsync();

        /// <summary>
        /// Get instance surface enforcement mode (off/warn/block).
        /// </summary>
        Task<SurfaceEnforcementSetting> GetSurfaceEnforcementAsync();

        /// <summary>
        /// Get custom command to deploy on launch.
        /// </summary>
        Task<string> GetCustomDeployOnLaunchAsync();

        /// <summary>
        /// Get true when the game should be launched with RenderDoc.
        /// </summary>
        Task<bool> GetLaunchRenderDocAsync();

        /// <summary>
        /// Get true when the game should be launched with RGP.
        /// </summary>
        Task<bool> GetLaunchRgpAsync();

        /// <summary>
        /// Get Vulkan driver variant to load on launch.
        /// </summary>
        Task<string> GetVulkanDriverVariantAsync();

        /// <summary>
        /// Get test account ID to use when launching.
        /// </summary>
        Task<string> GetTestAccountAsync();

        /// <summary>
        /// Get endpoint to be used when launching.
        /// </summary>
        Task<StadiaEndpoint> GetEndpointAsync();

        /// <summary>
        /// Get the query parameters to be appended to the URL when launching the chrome client
        /// or test client.
        /// </summary>
        Task<string> GetQueryParamsAsync();

        /// <summary>
        /// Get raw 'Deploy executable on launch' property value.
        /// </summary>
        Task<string> GetDeployExecutableOnLaunchRawAsync();

        /// <summary>
        /// Get raw 'Stadia instance surface enforcement' property value.
        /// </summary>
        Task<string> GetSurfaceEnforcementModeRawAsync();

        /// <summary>
        /// Get raw 'Launch with RenderDoc' property value.
        /// </summary>
        Task<string> GetLaunchWithRenderDocRawAsync();

        /// <summary>
        /// Get raw 'Launch with RGP' property value.
        /// </summary>
        Task<string> GetLaunchWithRgpRawAsync();

        /// <summary>
        /// Get raw 'Vulkan driver variant' property value.
        /// </summary>
        Task<string> GetVulkanDriverVariantRawAsync();

        /// <summary>
        /// Get raw 'Stadia Endpoint' property value.
        /// </summary>
        Task<string> GetStadiaEndpointRawAsync();
    }
}

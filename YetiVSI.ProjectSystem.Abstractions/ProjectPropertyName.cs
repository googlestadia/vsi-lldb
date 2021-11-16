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

namespace YetiVSI.ProjectSystem.Abstractions
{
    /// <summary>
    /// This class stores the names of properties relevant to Stadia used by the project system.
    /// </summary>
    public static class ProjectPropertyName
    {
        public const string ExecutablePath = "ExecutablePath";
        public const string OutputDirectory = "OutDir";
        public const string TargetPath = "TargetPath";
        public const string TargetFileName = "TargetName";

        public const string GgpApplication = "GgpApplication";
        public const string GgpCustomDeployOnLaunch = "GgpCustomDeployOnLaunch";
        public const string GgpDeployOnLaunch = "GgpDeployOnLaunch";
        public const string GgpSurfaceEnforcementMode = "GgpSurfaceEnforcementMode";
        public const string GgpGameletEnvironmentVariables = "GgpGameletEnvironmentVariables";
        public const string GgpGameletLaunchExecutable = "GgpGameletLaunchExecutable";
        public const string GgpGameletLaunchArguments = "GgpGameletLaunchArguments";
        public const string GgpLaunchRenderDoc = "GgpLaunchRenderDoc";
        public const string GgpLaunchRgp = "GgpLaunchRgp";
        public const string GgpLaunchDive = "GgpLaunchDive";
        public const string GgpLaunchOrbit = "GgpLaunchOrbit";
        public const string GgpVulkanDriverVariant = "GgpVulkanDriverVariant";
        public const string GgpTestAccount = "GgpTestAccount";
        public const string GgpExternalId = "GgpExternalId";
        public const string GgpEndpoint = "GgpEndpoint";
        public const string GgpQueryParams = "GgpQueryParams";

        public const string NMakeOutput = "NMakeOutput";
    }
}

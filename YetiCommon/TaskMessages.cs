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

namespace YetiCommon
{
    public static class TaskMessages
    {
        public const string EnablingSSH = "Enabling SSH...";
        public const string DownloadingCoreFile = "Downloading core file...";
        public const string CheckingBinaries = "Checking local and remote binaries...";
        public const string CheckingRemoteBinary = "Checking remote binary...";
        public const string CheckingMountInfo = "Checking mounts configuration...";
        public const string WaitingForGameStop = "Waiting for the game to stop...";
        public const string ClearingInstanceLogs = "Clearing instance logs...";
        public const string AttachingToProcess = "Attaching to the process...";
        public const string LaunchingGame = "Launching the game...";
        public const string LaunchingGrpcServer = "Launching the debugger process...";
        public const string LaunchingDeferredGameTitle = "Start playing";
        public const string LaunchingDeferredGame = "Your run has started on all player" +
            " endpoints. A prompt to play will appear on endpoints where you’re signed in. " +
            "You can switch endpoints at any time using these prompts, " +
            "and they’ll persist on all other endpoints unless you dismiss them.";

        public static string LaunchingDeferredGameWithExternalId(string appId) =>
            "Your run has started on all player endpoints. Go to the desired " +
            $"endpoint and launch using application ID {appId}.";

        public const string LookingForTheCurrentLaunch = "Looking for the current launch...";
        public const string DeployingOrbitVulkanLayer = "Deploying Orbit's Vulkan layer...";
        public const string DeployingExecutable = "Deploying executable...";
        public const string CustomDeployCommand = "Running custom deploy command...";
        public const string DeltaDeployEncode = "Computing executable diff...";
        public const string DeltaDeployDecode = "Restoring executable from diff...";
        public const string DeltaDeployCommand = "Deploying executable diff...";
        public const string DebuggerAttaching = "Debugger is attaching (this can take a while)";
        public const string DebuggerConnecting = "Connecting to debugger";
        public const string LoadingModulesDuringAttach = "Debugger is attaching: loading modules";

        public static string GetDeployingProgress(long sentBytes)
        {
            double sentMByte = sentBytes / _bytesPerMByte;
            return $"Deployed {sentMByte:F1} MB.";
        }

        public static string LaunchingDeferredGameRunFlow => $"{LaunchingDeferredGame}\r\n" +
            "If the prompt doesn't appear, the game has likely failed to start. " +
            "Run the game in debug mode to investigate the issue.";

        public static string LaunchingDeferredGameRunFlowWithExternalId(string appId) =>
            $"{LaunchingDeferredGameWithExternalId(appId)}\r\n" +
            "If you are unable to launch the game, the game has likely failed to start. " +
            "Run the game in debug mode to investigate the issue.";

        const double _bytesPerMByte = 1024 * 1024d;
    }
}

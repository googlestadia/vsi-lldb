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

﻿using GgpGrpc.Models;
using System;
using System.Collections.Generic;

namespace YetiVSI
{
    // Central location for error strings that may be displayed to the user, sorted alphabetically.
    public static class ErrorStrings
    {
        public const string AutoAttachNotSupported = "Auto attach is not supported.";
        public const string BinaryFileNameUnknown =
            "Unable to search for binary. Binary file name is unknown.";

        public const string CoreAttachBuildIdMissingWarningTitle = "Executable build id missing.";
        public const string CoreAttachBuildIdMissingWarningMessage =
            "The crash dump does not contain the build id of the executable. " +
            "If you continue to attach, " +
            "the debugger will try to find the executable by its name " +
            "in symbol search paths or in the same directory as the core dump. " +
            "If the executable does not match the one used in the core dump " +
            "the debugging experience will be degraded.\n\n" +
            "To avoid this message in the future, please make sure to build " +
            "the executable with a toolchain based on LLVM 9 or higher " +
            "and pass -Xlinker --build-id=sha1 parameters to the compiler.\n\n" +
            "Do you want to continue attaching?";

        public const string CoreFileDoesNotExist = "The specified core file does not exist.";
        public const string DialogTitleError = "Error";
        public const string DialogTitleWarning = "Warning";
        public const string FailedToCreateDebugger = "Failed to create LLDB SBDebugger object.";
        public const string FailedToCreateDebugListener =
            "Failed to create LLDB SBListener object.";
        public const string FailedToCreateDebugTarget = "Failed to create LLDB SBTarget object.";
        public const string FailedToCreateLldbPlatform = "Failed to create LLDB SBPlatform object.";
        public static string FailedToOpenLogsBecauseLoggingNotInitialized =
            "Failed to open logs, there is a problem with logging, please file a bug.";
        public const string FailedToRetrieveCoreFilePath =
            "Failed to retrieve path to core file. Please make sure a core file is selected.";
        public const string FailedToRetrieveProcessId =
            "Failed to retrieve process ID. " +
            "This likely means the game failed to start or was slow to start. " +
            "Make sure you are logged in to Chrome Client, and check the Chrome Client output " +
            "for more details.";
        public const string FailedToStartDebugger = "Failed to start the GGP Instance Debugger. " +
                                                    "Make sure the GGP SDK is installed correctly.";

        public const string CorruptedCoreFile = "Core Dump file appears to be corrupted.";
        public const string FailedToStartTransport =
            "Failed to start the GGP Instance Debugger " +
            "because a valid ID could not be generated. Please ensure there are less than 10 " +
            "instances of the GGP Instance Debugger running.";
        public const string FailedToStopGame =
            "Failed to stop game. Try closing the client window to stop the game session.";
        public const string ProcessExitedUnexpectedly =
            "A process required by the GGP Instance Debugger exited unexpectedly. " +
            "Please check the logs for more information.";
        public const string ModuleBuildIdUnknown =
            "Warning: The module's build ID is unknown. " +
            "This means that only flat directories can be searched, and it is not guaranteed " +
            "that the file found will match the module being debugged.";
        public const string NoApplicationConfigured =
            "No GGP Application is specified. Set " +
            "the 'GGP Application ID or Name' field in the 'Debugging' page in your project " +
            "properties. To see a list of available applications, run 'ggp application list'.";
        public const string NoCoreFileSelected = "Please select a core file to attach to.";
        public const string NoGameletsFound = "No instances found.  Run 'ggp instance reserve'.";
        public const string RpcFailure = "GRPC failure when communicating with the LLDB server. " +
            "Check the logs for more information.";
        public const string SingleProgramExpected =
            "Debug engine expects only a single program while attaching.";
        public const string SymbolFileNameUnknown =
            "Unable to search for symbols. Symbol file name is unknown.";
        public const string StopRunningGame = "Stop running game?";
        public const string GameNotRunningDuringAttach =
            "Cannot proceed with the attach. Game is not running.";
        public const string LaunchEndedCommonMessage = "Cannot proceed with the game launch. ";
        public const string LaunchEndedUnspecified = "Unknown reason.";
        public const string LaunchEndedExitedByUser = "User requested the game session to end.";
        public const string LaunchEndedInactivityTimeout = "Game shutdown due to user inactivity.";
        public const string LaunchEndedClientNeverConnected =
            "Game shutdown due to client never connecting to the session.";
        public const string LaunchEndedGameExitedWithSuccessfulCode =
            "Game process exited without an error code.";
        public const string LaunchEndedGameExitedWithErrorCode =
            "Game process exited with an error code, presumably crashed.";
        public const string LaunchEndedGameShutdownBySystem = "Game shutdown by the system.";
        public const string LaunchEndedUnexpectedGameShutdownBySystem =
            "Game unexpectedly shutdown, presumably by an issue with the system";
        public const string LaunchEndedGameBinaryNotFound = "The game binary was not found.";
        public const string LaunchEndedQueueAbandonedByUser =
            "The user explicitly requested to exit the queue.";
        public const string LaunchEndedQueueReadyTimeout =
            "The user didn't request to play when the queue was ready and it timed-out.";
        public const string GameNotStopped =
            "Could not stop the game. See logs for further details";
        public const string GameStoppedWithError =
            "Game was stopped, but the end reason was unexpected. ";
        public const string ThisInstance = "this instance";
        public static string LaunchExistsDialogText (string gameletName) =>
            $"A game is currently running on {gameletName}. Only one game can be running for" +
            " each player.\r\nWould you like to stop it?";
        public static string BuildIdCheckDetails(IEnumerable<string> matchedFiles,
                                                 IEnumerable<string> searchedPaths) =>
            $"Executables matched by name: " + Environment.NewLine
            + string.Join(Environment.NewLine, matchedFiles) + Environment.NewLine +
            $"Library search paths: " + Environment.NewLine
            + string.Join(Environment.NewLine, searchedPaths);
        public static string ErrorQueryingCoreFiles(string message) =>
            $"Failed to query instance crash dumps.{Environment.NewLine}{message}";
        public static string ErrorQueryingGameletProcesses(string message) =>
            $"Failed to query instance processes.{Environment.NewLine}{message}";
        public static string ErrorRunningCustomDeployCommand(string message) =>
            $"Error running custom deploy command.{Environment.NewLine}{message}";
        public static string ExecutableCheckDetails(IEnumerable<string> searchedPaths) =>
            $"Library search paths: " + Environment.NewLine
            + string.Join(Environment.NewLine, searchedPaths);
        public static string FailedToCheckRemoteBuildIdWithExplanation(string message) =>
            $"{message}{Environment.NewLine}" +
            $"A Build ID is required for various features of the GGP Debugger to work properly. " +
            $"For more information about Build IDs, see the document 'Debug in Visual Studio'.";
        public static string FailedToDeployExecutable(string message) =>
            $"Failed to deploy executable to your instance.{Environment.NewLine}{message}";
        public static string FailedToDeployExecutableMissingUploadDir(string uploadDir) =>
            "Failed to deploy executable to your instance because the upload directory " +
            $"'{uploadDir}' is missing on the instance. {Environment.NewLine}Ensure that " +
            "your instance is a developer shape instance. For more information about instance " +
            "shapes, see the document 'Choose the right instance'.";
        public static string FailedToSetExecutablePermissions(string message) =>
            $"Failed to set executable permissions.{Environment.NewLine}{message}";
        public static string FailedToEnableSsh(string message) =>
            $"Failed to enable SSH on your instance.{Environment.NewLine}{message}";
        public static string FailedToStartRequiredProcess(string message) =>
            $"Failed to start a process required by the GGP Instance Debugger." +
            $"{Environment.NewLine}{message}";
        public static string FailedToAttachToProcess(string lldbError) =>
            $"Failed to attach to process. Internal LLDB error '{lldbError}'.";
        public static string FailedToAttachToProcessSelfTrace =>
            "Failed to attach to process. The process is already tracing itself using " +
            "the ptrace(PTRACE_TRACEME, ...) API call. The debugger cannot attach because " +
            "the OS allows at most one tracer/debugger per process." +
            $"{Environment.NewLine}{Environment.NewLine}" +
            "To make the process debuggable, make sure your process is not traced when ataching.";
        public static string FailedToAttachToProcessOtherTracer(string tracerName,
                                                                string tracerPid) =>
            "Failed to attach to the process because the process is already being traced. " +
            "The OS allows at most one tracer/debugger per process." +
            $"{Environment.NewLine}{Environment.NewLine}" +
            $"The currently attached tracer process is \"{tracerName}\" (process ID {tracerPid}).";
        public static string FailedToConnectDebugger(string url) =>
            $"Failed to connect to the remote debugger at {url}. " +
            $"Check your firewall and proxy settings to make sure this port isn't being blocked " +
            $"or proxied.";
        public static string FailedToLoadCore(string coreFilePath) =>
            $"Failed to load core file: '{coreFilePath}'";
        public static string FailedToDownloadCore(string message) =>
            $"Failed to download core file. {message}";
        public static string FailedToFindFile(string filename) => $"Failed to find file {filename}";
        public static string FailedToLoadBinary(string binaryPath) =>
            $"Failed to load binary '{binaryPath}'";
        public static string FailedToOpenLogsBecauseLogFileMayNotExist(string path) =>
            $"Failed to open logs, {path} may not exist.";
        public static string FailedToRetrieveApplication(string application) =>
            "Something went " +
            $"wrong retrieving your application {application}. Please try again or contact your " +
            "Technical Account Manager.";
        public static string FailedToRetrieveGamelets(string message) =>
            $"Failed to retrieve your reserved instances.{Environment.NewLine}{message}";
        public static string FileNotOnFilesystem(string fileUrl) =>
            $"Unable to load file. '{fileUrl}' must be cached in a filesystem " +
            $"location. Ensure that a valid cache directory is set in 'Tools -> Options -> " +
            $"Debugging -> Symbols'.";
        public static string GameletInUnexpectedState(Gamelet gamelet) =>
            $"Game cannot be launched as instance '{gamelet.DisplayName}' with ID '{gamelet.Id}' " +
            $"is in an unexpected state: {gamelet.State.ToString()}. Please wait a few seconds " +
            "and try again. If this issue persists, reboot your instance using 'ggp instance " +
            "reboot', or try a different instance.";
        public static string InvalidBinaryPathOrName(string binaryPath, string binaryName,
                                                     string message) =>
            $"Invalid binary path '{binaryPath}' or name '{binaryName}'. {message}";
        public static string InvalidLaunchOption(string launchOption) =>
            $"Invalid launch option: {launchOption}";
        public static string InvalidTestAccount(string testAccountId) =>
            $"Invalid test account: {testAccountId}. Set the 'Test Account' field in the " +
            "'Debugging' page in your project properties. To see a list of available test " +
            "accounts, run 'ggp test-account list'.";
        public static string MoreThanOneTestAccount(string stadiaName) =>
            $"More than one test account exists with the chosen Stadia Name {stadiaName}. Run " +
            $"'ggp test-account describe {stadiaName}' to list all test accounts with this name, " +
            $"and set the 'Test Account' field in the 'Debugging' page in your project " +
            $"properties using either an ID or Stadia Name with its numerical ID.";
        public static string UnableToFindExecutable(string executable) =>
            $"Failed to find executable '{executable}'. This can cause longer than normal " +
            $"launch times and missing symbols. Please make sure your 'Output Directory' and " +
            $"'Target Name' or NMake 'Output' project properties are set correctly.";
        public static string UnableToFindExecutableMatchingRemoteBinary(string executable,
                                                                        string remotePath) =>
            $"Could not find a local copy of {executable} matching the build ID of the remote " +
            $"binary '{remotePath}'.{Environment.NewLine} " +
            $"This can cause longer than normal launch times and missing symbols. Please make " +
            $"sure your 'Output Directory' and 'Target Name' or NMake 'Output' project " +
            $"properties are set correctly. This could also indicate a problem with any custom " +
            $"deploy scripts that you may be using.{Environment.NewLine}" +
            $"For more information about Build IDs, see the document 'Debug in Visual Studio'.";
        public static string UnableToFindTargetExecutable(string targetPath) =>
            $"Failed to find executable: {targetPath}{Environment.NewLine}Make sure that the " +
            $"'Output Directory' and 'Target Name' project properties are set correctly, the " +
            $"'Target Extension' project property is blank, and the executable has the correct " +
            $"permissions.";
        public static string MountConfigurationWarning(string gameAssetsPath,
                                                       string developerMountPath) =>
            $"The game directory {gameAssetsPath} contains a mounted package. As a result, " +
            $"any binary uploaded to the upload directory {developerMountPath} " +
            $"will be ignored.{Environment.NewLine}If this is unintended please run " +
            "'ggp instance unmount' to reset the gamelet mount configuration." +
            $"{Environment.NewLine}Do you wish to continue?";

        public static string AssetStreamingBrokenWarning() =>
            "This gamelet has asset streaming activated, but no corresponding asset streaming " +
            "service appears to be running. Please validate the connection and restart asset " +
            $"streaming if needed.{Environment.NewLine}Continue launch anyway?";

        public static string AssetStreamingDeployWarning(
            string folder, string current, string expected) =>
            $"This gamelet is streaming assets from the folder: '{folder}' which is also " +
            $"used as binary output folder, and automatic deployment is set to '{current}'. " +
            $"Please consider changing automatic deployment to '{expected}' in the project " +
            "properties to avoid issues during the deployment process (please see " +
            "'Properties'->'Configuration Properties'->'Debugging'->'Deploy Executable on " +
            $"Launch').{Environment.NewLine}Continue launch anyway?";
    }
}

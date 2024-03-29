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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using Metrics.Shared;
using YetiCommon;
using YetiCommon.SSH;
using YetiVSI.Metrics;

namespace YetiVSI.DebugEngine
{
    public class PreflightBinaryCheckerException : Exception, IUserVisibleError
    {
        public PreflightBinaryCheckerException(string msg, Exception inEx, bool isCritical) : base(
            msg, inEx)
        {
            IsCritical = isCritical;
        }

        public PreflightBinaryCheckerException(string msg, string userDetails, Exception inEx,
                                               bool isCritical) : base(msg, inEx)
        {
            UserDetails = userDetails;
            IsCritical = isCritical;
        }

        public string UserDetails { get; }

        /// <summary>
        /// If true, the exception is treated as a critical error that stops the launch/attach flow.
        /// </summary>
        public bool IsCritical { get; }
    }

    /// <summary>
    /// Provides methods to verify local and remote binaries before starting a debug session.
    /// </summary>
    public interface IPreflightBinaryChecker
    {
        /// <summary>
        /// Check that the remote binary for the given executable exists and has a valid build id.
        /// Look for a local copy of the binary based on the name and build id. Log messages and
        /// record metrics to indicate the result of the checks.
        /// </summary>
        /// <param name="libPaths">LLDB search paths to check for local binaries.</param>
        /// <param name="executable">Name of the binary to look for locally and remotely.</param>
        /// <param name="target">The machine that should have a valid remote binary.</param>
        /// <param name="remoteTargetPaths">Remote paths where the binary is expected to be.
        /// </param>
        /// <param name="action">An action to be recorded as a metrics log event.</param>
        /// <param name="moduleFormat">Format of the module. It can be elf, pdb or pe.</param>
        Task CheckLocalAndRemoteBinaryOnLaunchAsync(ISet<string> libPaths, string executable,
                                                    SshTarget target,
                                                    List<string> remoteTargetPaths, IAction action,
                                                    ModuleFormat moduleFormat);

        /// <summary>
        /// Check that the specified remote process's binary has a valid build id. Log messages and
        /// record metrics to indicate the result of the checks.
        /// </summary>
        /// <param name="pid">Process ID of the remote process that we will check</param>
        /// <param name="target">The machine that should have a valid remote binary</param>
        /// <param name="action">An action to be recorded as a metrics log event.</param>
        Task CheckRemoteBinaryOnAttachAsync(uint pid, SshTarget target, IAction action);
    }

    public class PreflightBinaryChecker : IPreflightBinaryChecker
    {
        const string _pidExePathTemplate = "/proc/{0}/exe";

        readonly IFileSystem _fileSystem;
        readonly IModuleParser _moduleParser;

        public PreflightBinaryChecker(IFileSystem fileSystem, IModuleParser moduleParser)
        {
            _fileSystem = fileSystem;
            _moduleParser = moduleParser;
        }

        public async Task CheckLocalAndRemoteBinaryOnLaunchAsync(
            ISet<string> libPaths, string executable, SshTarget target,
            List<string> remoteTargetPaths, IAction action, ModuleFormat moduleFormat)
        {
            // Check that the remote binary has a build id and try to match it against
            // the local candidates to find the matching local binary.
            var dataRecorder =
                new DataRecorder(action, DebugPreflightCheckData.Types.CheckType.RunAndAttach);
            BuildId remoteBuildId = null;
            string remoteTargetPath = null;
            foreach (string pathCandidate in remoteTargetPaths)
            {
                try
                {
                    // Get the remote build id and only continue if this step succeeds.
                    remoteBuildId =
                        await _moduleParser.ParseRemoteBuildIdInfoAsync(pathCandidate, target);
                    Trace.WriteLine($"Found remote binary '{pathCandidate}'");
                    remoteTargetPath = pathCandidate;
                    break;
                }
                catch (BinaryFileUtilException e)
                {
                    if (e.InnerException is FileNotFoundException)
                    {
                        // Continue to next path candidate.
                        continue;
                    }

                    dataRecorder.RemoteBuildIdError(e);
                    Trace.WriteLine($"Failed to read build ID for '{pathCandidate}' " +
                                    $"on '{target.GetString()}': {e.Demystify()}");
                    throw new PreflightBinaryCheckerException(
                        ErrorStrings.FailedToCheckRemoteBuildIdWithExplanation(e.Message), e,
                        isCritical: false);
                }
            }

            if (remoteTargetPath == null)
            {
                // Binary was not found in any of the remoteTargetPaths.
                dataRecorder.RemoteFileNotFoundError();
                Trace.WriteLine("Failed to find remote binary in any of " +
                                $"'{string.Join("', '", remoteTargetPaths)}' ");
                throw new PreflightBinaryCheckerException(
                    ErrorStrings.LaunchEndedGameBinaryNotFound, new NoRemoteBinaryException(),
                    isCritical: true);
            }

            // Log the remote Build ID for debugging purposes.
            dataRecorder.ValidRemoteBuildId();
            Trace.WriteLine($"Remote build ID: {remoteBuildId?.ToPathName(moduleFormat)}");

            // Make sure there is a local binary with the same name.
            List<string> localCandidatePaths = FindExecutableCandidates(libPaths, executable);
            if (localCandidatePaths.Count == 0)
            {
                dataRecorder.LocalBinaryCheckResult(
                    DebugPreflightCheckData.Types.LocalBinarySearchResult.NoCandidates);
                Trace.WriteLine($"Unable to find executable '{executable}' on LLDB search paths.");
                throw new PreflightBinaryCheckerException(
                    ErrorStrings.UnableToFindExecutable(executable),
                    ErrorStrings.ExecutableCheckDetails(libPaths), new NoLocalCandidatesException(),
                    isCritical: false);
            }

            // Check local candidates to find one matching the remote build id.
            // Ignore local candidates that are missing a build id.
            if (HasMatchingBuildId(localCandidatePaths, executable, remoteTargetPath, remoteBuildId,
                                   moduleFormat))
            {
                dataRecorder.LocalBinaryCheckResult(
                    DebugPreflightCheckData.Types.LocalBinarySearchResult.BinaryMatch);
            }
            else
            {
                dataRecorder.LocalBinaryCheckResult(
                    DebugPreflightCheckData.Types.LocalBinarySearchResult.BinaryMismatch);
                Trace.WriteLine(
                    $"No local copy of '{executable}' matched the build ID of the remote binary");
                throw new PreflightBinaryCheckerException(
                    ErrorStrings.UnableToFindExecutableMatchingRemoteBinary(executable,
                        remoteTargetPath),
                    ErrorStrings.BuildIdCheckDetails(localCandidatePaths, libPaths),
                    new NoMatchingLocalCandidatesException(), isCritical: false);
            }
        }

        public async Task CheckRemoteBinaryOnAttachAsync(uint pid, SshTarget target, IAction action)
        {
            string remoteTargetPath = string.Format(_pidExePathTemplate, pid);
            var dataRecorder =
                new DataRecorder(action, DebugPreflightCheckData.Types.CheckType.AttachOnly);
            try
            {
                BuildId remoteBuildId =
                    await _moduleParser.ParseRemoteBuildIdInfoAsync(remoteTargetPath, target);
                // Log the remote Build ID for debugging purposes.
                dataRecorder.ValidRemoteBuildId();
                Trace.WriteLine($"Remote build ID: {remoteBuildId}");
            }
            catch (BinaryFileUtilException e)
            {
                dataRecorder.RemoteBuildIdError(e);
                Trace.WriteLine($"Failed to read build ID for '{remoteTargetPath}' " +
                                $"on '{target.GetString()}': {e.Demystify()}");

                throw new PreflightBinaryCheckerException(
                    ErrorStrings.FailedToCheckRemoteBuildIdWithExplanation(e.Message), e,
                    isCritical: false);
            }
        }

        bool HasMatchingBuildId(IEnumerable<string> localCandidatePaths, string executable,
                                string remoteTargetPath, BuildId remoteBuildId,
                                ModuleFormat moduleFormat)
        {
            // TODO: re-write this using LINQ and Optional<T, TException>
            foreach (string path in localCandidatePaths)
            {
                BuildIdInfo localBuildId = _moduleParser.ParseBuildIdInfo(path, ModuleFormat.Elf);
                if (localBuildId.HasError)
                {
                    Trace.WriteLine($"Failed to read build ID for '{path}': {localBuildId.Error}");
                    continue;
                }

                if (localBuildId.Data.Matches(remoteBuildId, moduleFormat))
                {
                    Trace.WriteLine($"Found local copy of '{executable}' at '{path}' " +
                                    $"matching build ID of remote binary '{remoteTargetPath}'");
                    return true;
                }

                Trace.WriteLine($"Mismatched build ID {localBuildId.Data} " +
                                $"for local binary '{path}' " + $"and build ID {remoteBuildId} " +
                                $"for remote binary {remoteTargetPath}");
            }

            return false;
        }

        List<string> FindExecutableCandidates(IEnumerable<string> executablePaths,
                                              string executableName)
        {
            return executablePaths
                .Select(path => Path.Combine(FileUtil.RemoveQuotesFromPath(path),
                                             FileUtil.RemoveQuotesFromPath(executableName)))
                .Where(file => _fileSystem.File.Exists(file)).ToList();
        }

        class NoLocalCandidatesException : ConfigurationException
        {
            public NoLocalCandidatesException() : base("No local candidates")
            {
            }
        }

        class NoMatchingLocalCandidatesException : ConfigurationException
        {
            public NoMatchingLocalCandidatesException() : base("No matching local candidates")
            {
            }
        }

        class NoRemoteBinaryException : ConfigurationException
        {
            public NoRemoteBinaryException() : base("No remote binary")
            {
            }
        }

        // Helper class to record various metrics about preflight checks. This helps abstract
        // away some proto-handling details in the main workflow.
        class DataRecorder
        {
            readonly IAction _action;

            public DataRecorder(IAction action, DebugPreflightCheckData.Types.CheckType type)
            {
                _action = action;
                RecordData(new DebugPreflightCheckData() { CheckType = type });
            }

            public void ValidRemoteBuildId()
            {
                RecordData(new DebugPreflightCheckData()
                {
                    RemoteBuildIdCheckResult = DebugPreflightCheckData.Types
                        .RemoteBuildIdCheckResult.ValidRemoteBuildId
                });
            }

            public void RemoteBuildIdError(BinaryFileUtilException e)
            {
                var data = new DebugPreflightCheckData();
                if (e is InvalidBuildIdException)
                {
                    data.RemoteBuildIdCheckResult = DebugPreflightCheckData.Types
                        .RemoteBuildIdCheckResult.InvalidRemoteBuildId;
                }
                else
                {
                    switch (e.InnerException)
                    {
                        case ProcessExecutionException _:
                            data.RemoteBuildIdCheckResult = DebugPreflightCheckData.Types
                                .RemoteBuildIdCheckResult.RemoteBinaryError;
                            break;
                        case ProcessException _:
                            data.RemoteBuildIdCheckResult = DebugPreflightCheckData.Types
                                .RemoteBuildIdCheckResult.RemoteCommandError;
                            break;
                    }
                }

                RecordData(data);
            }

            public void RemoteFileNotFoundError()
            {
                RecordData(new DebugPreflightCheckData()
                {
                    RemoteBuildIdCheckResult = DebugPreflightCheckData.Types
                        .RemoteBuildIdCheckResult.RemoteBinaryError
                });
            }

            public void LocalBinaryCheckResult(
                DebugPreflightCheckData.Types.LocalBinarySearchResult result)
            {
                RecordData(new DebugPreflightCheckData() { LocalBinarySearchResult = result });
            }

            void RecordData(DebugPreflightCheckData data)
            {
                _action.UpdateEvent(new DeveloperLogEvent() { DebugPreflightCheckData = data });
            }
        }
    }
}
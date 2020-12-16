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

﻿using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YetiCommon;
using YetiCommon.SSH;
using YetiVSI.DebugEngine;
using YetiVSI.Metrics;
using YetiVSI.Shared.Metrics;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class PreflightBinaryCheckerTests
    {
        MockFileSystem fileSystem;
        IBinaryFileUtil binaryFileUtil;
        IMetrics metrics;
        IAction action;
        PreflightBinaryChecker checker;

        // Note: we don't strictly require the executable file names to be the same.
        static readonly string executable = "foo.elf";
        static readonly uint remoteTargetPid = 1234;
        static readonly string remoteTargetPath = "/proc/1234/exe";

        static readonly SshTarget target = new SshTarget("127.0.0.1:22");
        static readonly BuildId validBuildId = new BuildId("AA");
        static readonly BuildId validBuildId2 = new BuildId("BB");
        static readonly string[] searchPaths = { "/path1", "/path2" };
        static readonly List<string> localPaths =
            searchPaths.Select(path => Path.Combine(path, executable)).ToList();

        [SetUp]
        public void SetUp()
        {
            fileSystem = new MockFileSystem();
            binaryFileUtil = Substitute.For<IBinaryFileUtil>();
            metrics = Substitute.For<IMetrics>();
            checker = new PreflightBinaryChecker(fileSystem, binaryFileUtil);
            action = new ActionRecorder(metrics).CreateToolAction(
                ActionType.DebugPreflightBinaryChecks);
        }

        [Test]
        public async Task CheckRemoteBinaryOnAttachSucceedsAsync()
        {
            binaryFileUtil.ReadBuildIdAsync(remoteTargetPath, target)
                .Returns(Task.FromResult(validBuildId));

            await action.RecordAsync(checker.CheckRemoteBinaryOnAttachAsync(remoteTargetPid,
                target, action));

            metrics.Received().RecordEvent(
                DeveloperEventType.Types.Type.VsiDebugPreflightBinaryCheck,
                Arg.Is<DeveloperLogEvent>(m =>
                    m.StatusCode == DeveloperEventStatus.Types.Code.Success &&
                    m.DebugPreflightCheckData.CheckType ==
                        DebugPreflightCheckData.Types.CheckType.AttachOnly &&
                    m.DebugPreflightCheckData.RemoteBuildIdCheckResult ==
                        DebugPreflightCheckData.Types.RemoteBuildIdCheckResult
                            .ValidRemoteBuildId));
        }

        [Test]
        public void CheckRemoteBinaryFailsInvalidBuildId()
        {
            binaryFileUtil.ReadBuildIdAsync(remoteTargetPath, target).Returns(
                Task.FromException<BuildId>(new InvalidBuildIdException("test")));

            Assert.ThrowsAsync<PreflightBinaryCheckerException>(async () =>
                await action.RecordAsync(checker.CheckRemoteBinaryOnAttachAsync(remoteTargetPid,
                    target, action)));

            metrics.Received().RecordEvent(
                DeveloperEventType.Types.Type.VsiDebugPreflightBinaryCheck,
                Arg.Is<DeveloperLogEvent>(m =>
                    m.StatusCode == DeveloperEventStatus.Types.Code.InvalidConfiguration &&
                    m.DebugPreflightCheckData.CheckType ==
                        DebugPreflightCheckData.Types.CheckType.AttachOnly &&
                    m.DebugPreflightCheckData.RemoteBuildIdCheckResult ==
                        DebugPreflightCheckData.Types.RemoteBuildIdCheckResult
                            .InvalidRemoteBuildId));
        }

        [Test]
        public void CheckRemoteBinaryFailsToReadBuildId()
        {
            binaryFileUtil.ReadBuildIdAsync(remoteTargetPath, target).Returns(
                Task.FromException<BuildId>(new BinaryFileUtilException("test",
                    new ProcessExecutionException("inner", 1))));

            Exception ex = Assert.ThrowsAsync<PreflightBinaryCheckerException>(async () =>
                await action.RecordAsync(checker.CheckRemoteBinaryOnAttachAsync(remoteTargetPid,
                    target, action)));
            Assert.IsInstanceOf<BinaryFileUtilException>(ex.InnerException);

            metrics.Received().RecordEvent(
                DeveloperEventType.Types.Type.VsiDebugPreflightBinaryCheck,
                Arg.Is<DeveloperLogEvent>(m =>
                    m.StatusCode ==
                        DeveloperEventStatus.Types.Code.ExternalToolFailure &&
                    m.DebugPreflightCheckData.CheckType ==
                        DebugPreflightCheckData.Types.CheckType.AttachOnly &&
                    m.DebugPreflightCheckData.RemoteBuildIdCheckResult ==
                        DebugPreflightCheckData.Types.RemoteBuildIdCheckResult
                            .RemoteBinaryError));
        }

        [Test]
        public void CheckRemoteBinaryFailsToRunRemoteCommand()
        {
            binaryFileUtil.ReadBuildIdAsync(remoteTargetPath, target).Returns(
                Task.FromException<BuildId>(new BinaryFileUtilException("test",
                    new ProcessException("inner"))));

            Exception ex = Assert.ThrowsAsync<PreflightBinaryCheckerException>(async () =>
                await action.RecordAsync(checker.CheckRemoteBinaryOnAttachAsync(remoteTargetPid,
                    target, action)));
            Assert.IsInstanceOf<BinaryFileUtilException>(ex.InnerException);
            Assert.AreEqual(ErrorStrings.FailedToCheckRemoteBuildIdWithExplanation(
                ex.InnerException.Message), ex.Message);

            metrics.Received().RecordEvent(
                DeveloperEventType.Types.Type.VsiDebugPreflightBinaryCheck,
                Arg.Is<DeveloperLogEvent>(m =>
                    m.StatusCode ==
                        DeveloperEventStatus.Types.Code.ExternalToolUnavailable &&
                    m.DebugPreflightCheckData.CheckType ==
                        DebugPreflightCheckData.Types.CheckType.AttachOnly &&
                    m.DebugPreflightCheckData.RemoteBuildIdCheckResult ==
                        DebugPreflightCheckData.Types.RemoteBuildIdCheckResult
                            .RemoteCommandError));
        }

        [Test]
        public async Task CheckLocalAndRemoteBinarySucceedsAsync()
        {
            fileSystem.AddDirectory(searchPaths[0]);
            fileSystem.AddFile(localPaths[0], new MockFileData(""));
            fileSystem.AddDirectory(searchPaths[1]);
            fileSystem.AddFile(localPaths[1], new MockFileData(""));

            // Make the 2nd local file match the remote file, to force skipping the first file.
            binaryFileUtil.ReadBuildIdAsync(remoteTargetPath, target).Returns(
                Task.FromResult(validBuildId));
            binaryFileUtil.ReadBuildIdAsync(localPaths[0]).Returns(Task.FromResult(validBuildId2));
            binaryFileUtil.ReadBuildIdAsync(localPaths[1]).Returns(Task.FromResult(validBuildId));

            await action.RecordAsync(checker.CheckLocalAndRemoteBinaryOnLaunchAsync(
                searchPaths, executable, target, remoteTargetPath, action));

            metrics.Received().RecordEvent(
                DeveloperEventType.Types.Type.VsiDebugPreflightBinaryCheck,
                Arg.Is<DeveloperLogEvent>(m =>
                    m.StatusCode == DeveloperEventStatus.Types.Code.Success &&
                    m.DebugPreflightCheckData.CheckType ==
                        DebugPreflightCheckData.Types.CheckType.RunAndAttach &&
                    m.DebugPreflightCheckData.RemoteBuildIdCheckResult ==
                        DebugPreflightCheckData.Types.RemoteBuildIdCheckResult
                            .ValidRemoteBuildId &&
                    m.DebugPreflightCheckData.LocalBinarySearchResult ==
                        DebugPreflightCheckData.Types.LocalBinarySearchResult.BinaryMatch));
        }

        [Test]
        public void CheckLocalAndRemoteBinaryFailsNoCandidates()
        {
            binaryFileUtil.ReadBuildIdAsync(remoteTargetPath, target).Returns(
                Task.FromResult(validBuildId));

            Exception ex = Assert.ThrowsAsync<PreflightBinaryCheckerException>(async () =>
                await action.RecordAsync(checker.CheckLocalAndRemoteBinaryOnLaunchAsync(
                searchPaths, executable, target, remoteTargetPath, action)));
            Assert.AreEqual(ErrorStrings.UnableToFindExecutable(executable), ex.Message);

            metrics.Received().RecordEvent(
                DeveloperEventType.Types.Type.VsiDebugPreflightBinaryCheck,
                Arg.Is<DeveloperLogEvent>(m =>
                    m.StatusCode == DeveloperEventStatus.Types.Code.InvalidConfiguration &&
                    m.DebugPreflightCheckData.CheckType ==
                        DebugPreflightCheckData.Types.CheckType.RunAndAttach &&
                    m.DebugPreflightCheckData.RemoteBuildIdCheckResult ==
                        DebugPreflightCheckData.Types.RemoteBuildIdCheckResult
                            .ValidRemoteBuildId &&
                    m.DebugPreflightCheckData.LocalBinarySearchResult ==
                        DebugPreflightCheckData.Types.LocalBinarySearchResult.NoCandidates));
        }

        [Test]
        public void CheckLocalAndRemoteBinaryFailsInvalidRemoteBuildId()
        {
            fileSystem.AddDirectory(searchPaths[0]);
            fileSystem.AddFile(localPaths[0], new MockFileData(""));
            binaryFileUtil.ReadBuildIdAsync(localPaths[0]).Returns(Task.FromResult(validBuildId));

            binaryFileUtil.ReadBuildIdAsync(remoteTargetPath, target).Returns(
                Task.FromException<BuildId>(new InvalidBuildIdException("test")));

            Exception ex = Assert.ThrowsAsync<PreflightBinaryCheckerException>(async () =>
                await action.RecordAsync(checker.CheckLocalAndRemoteBinaryOnLaunchAsync(
                    searchPaths, executable, target, remoteTargetPath, action)));
            Assert.IsInstanceOf<BinaryFileUtilException>(ex.InnerException);
            Assert.AreEqual(ErrorStrings.FailedToCheckRemoteBuildIdWithExplanation(
                ex.InnerException.Message), ex.Message);

            metrics.Received().RecordEvent(
                DeveloperEventType.Types.Type.VsiDebugPreflightBinaryCheck,
                Arg.Is<DeveloperLogEvent>(m =>
                    m.StatusCode == DeveloperEventStatus.Types.Code.InvalidConfiguration &&
                    m.DebugPreflightCheckData.CheckType ==
                        DebugPreflightCheckData.Types.CheckType.RunAndAttach &&
                    m.DebugPreflightCheckData.RemoteBuildIdCheckResult ==
                        DebugPreflightCheckData.Types.RemoteBuildIdCheckResult
                            .InvalidRemoteBuildId));
        }

        [Test]
        public void CheckLocalAndRemoteBinaryFailsReadLocal()
        {
            fileSystem.AddDirectory(searchPaths[0]);
            fileSystem.AddFile(localPaths[0], new MockFileData(""));
            binaryFileUtil.ReadBuildIdAsync(localPaths[0])
                .Returns(Task.FromException<BuildId>(new BinaryFileUtilException("test")));

            binaryFileUtil.ReadBuildIdAsync(remoteTargetPath, target).Returns(
                Task.FromResult(validBuildId));

            Exception ex = Assert.ThrowsAsync<PreflightBinaryCheckerException>(async () =>
                await action.RecordAsync(checker.CheckLocalAndRemoteBinaryOnLaunchAsync(
                    searchPaths, executable, target, remoteTargetPath, action)));
            Assert.AreEqual(ErrorStrings.UnableToFindExecutableMatchingRemoteBinary(executable,
                remoteTargetPath), ex.Message);


            // Note that the local build id failure results in an invalid configuration error
            // because we don't propagate the original BinaryFileUtilException but rather throw
            // our own ConfigurationException. (We may have failed on more than one file.)
            metrics.Received().RecordEvent(
                DeveloperEventType.Types.Type.VsiDebugPreflightBinaryCheck,
                Arg.Is<DeveloperLogEvent>(m =>
                    m.StatusCode == DeveloperEventStatus.Types.Code.InvalidConfiguration &&
                    m.DebugPreflightCheckData.CheckType ==
                        DebugPreflightCheckData.Types.CheckType.RunAndAttach &&
                    m.DebugPreflightCheckData.RemoteBuildIdCheckResult ==
                        DebugPreflightCheckData.Types.RemoteBuildIdCheckResult
                            .ValidRemoteBuildId &&
                    m.DebugPreflightCheckData.LocalBinarySearchResult ==
                        DebugPreflightCheckData.Types.LocalBinarySearchResult.BinaryMismatch));
        }

        [Test]
        public void CheckLocalAndRemoteBinaryFailsMismatch()
        {
            fileSystem.AddDirectory(searchPaths[0]);
            fileSystem.AddFile(localPaths[0], new MockFileData(""));
            binaryFileUtil.ReadBuildIdAsync(localPaths[0]).Returns(Task.FromResult(validBuildId2));

            binaryFileUtil.ReadBuildIdAsync(remoteTargetPath, target).Returns(
                Task.FromResult(validBuildId));

            Exception ex = Assert.ThrowsAsync<PreflightBinaryCheckerException>(async () =>
                await action.RecordAsync(checker.CheckLocalAndRemoteBinaryOnLaunchAsync(
                    searchPaths, executable, target, remoteTargetPath, action)));
            Assert.AreEqual(ErrorStrings.UnableToFindExecutableMatchingRemoteBinary(executable,
                remoteTargetPath), ex.Message);

            metrics.Received().RecordEvent(
                DeveloperEventType.Types.Type.VsiDebugPreflightBinaryCheck,
                Arg.Is<DeveloperLogEvent>(m =>
                    m.StatusCode == DeveloperEventStatus.Types.Code.InvalidConfiguration &&
                    m.DebugPreflightCheckData.CheckType ==
                        DebugPreflightCheckData.Types.CheckType.RunAndAttach &&
                    m.DebugPreflightCheckData.RemoteBuildIdCheckResult ==
                        DebugPreflightCheckData.Types.RemoteBuildIdCheckResult
                            .ValidRemoteBuildId &&
                    m.DebugPreflightCheckData.LocalBinarySearchResult ==
                        DebugPreflightCheckData.Types.LocalBinarySearchResult.BinaryMismatch));
        }

        Expression<Predicate<string>> MatchWithParam(
            Func<string, string> messageWithParams)
        {
            return s => Regex.IsMatch(s, messageWithParams("(.+)"));
        }

        Expression<Predicate<string>> MatchWithParam(
            Func<string, string, string> messageWithParams)
        {
            return s => Regex.IsMatch(s, messageWithParams("(.+)", "(.+)"));
        }
    }
}

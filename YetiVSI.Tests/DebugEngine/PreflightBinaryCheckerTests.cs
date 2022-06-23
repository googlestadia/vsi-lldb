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
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using Metrics.Shared;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using YetiCommon;
using YetiCommon.SSH;
using YetiVSI.DebugEngine;
using YetiVSI.Metrics;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class PreflightBinaryCheckerTests
    {
        MockFileSystem _fileSystem;
        IModuleParser _moduleParser;
        IVsiMetrics _metrics;
        IAction _action;
        PreflightBinaryChecker _checker;

        // Note: we don't strictly require the executable file names to be the same.
        static readonly string _executable = "foo.elf";
        static readonly uint _remoteTargetPid = 1234;
        static readonly string _remoteTargetPath = "/proc/1234/exe";
        static readonly List<string> _remoteTargetPaths = new List<string> { _remoteTargetPath };

        static readonly SshTarget _target = new SshTarget("127.0.0.1:22");
        static readonly BuildId _validBuildId = new BuildId("AA");
        static readonly BuildId _validBuildId2 = new BuildId("BB");
        static readonly HashSet<string> _searchPaths = new HashSet<string> { "/path1", "/path2" };
        static readonly List<string> _localPaths =
            _searchPaths.Select(path => Path.Combine(path, _executable)).ToList();

        [SetUp]
        public void SetUp()
        {
            _fileSystem = new MockFileSystem();
            _moduleParser = Substitute.For<IModuleParser>();
            _metrics = Substitute.For<IVsiMetrics>();
            _checker = new PreflightBinaryChecker(_fileSystem, _moduleParser);
            _action = new ActionRecorder(_metrics).CreateToolAction(
                ActionType.DebugPreflightBinaryChecks);
        }

        [Test]
        public async Task CheckRemoteBinaryOnAttachSucceedsAsync()
        {
            await _action.RecordAsync(_checker.CheckRemoteBinaryOnAttachAsync(_remoteTargetPid,
                                          _target, _action));

            _metrics.Received().RecordEvent(
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
            _moduleParser.ParseRemoteBuildIdInfoAsync(_remoteTargetPath, _target).Returns(
                Task.FromException<BuildId>(new InvalidBuildIdException("test")));
            Assert.ThrowsAsync<PreflightBinaryCheckerException>(async () =>
                await _action.RecordAsync(_checker.CheckRemoteBinaryOnAttachAsync(_remoteTargetPid,
                                              _target, _action)));

            _metrics.Received().RecordEvent(
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
            _moduleParser.ParseRemoteBuildIdInfoAsync(_remoteTargetPath, _target).Returns(
                Task.FromException<BuildId>(
                    new BinaryFileUtilException(
                        "test", new ProcessExecutionException("inner", 1))));

            Exception ex = Assert.ThrowsAsync<PreflightBinaryCheckerException>(async () =>
                await _action.RecordAsync(_checker.CheckRemoteBinaryOnAttachAsync(_remoteTargetPid,
                    _target, _action)));
            Assert.IsInstanceOf<BinaryFileUtilException>(ex.InnerException);

            _metrics.Received().RecordEvent(
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
            _moduleParser.ParseRemoteBuildIdInfoAsync(_remoteTargetPath, _target)
                .Returns(Task.FromException<BuildId>(
                             new BinaryFileUtilException(
                                 "test", new ProcessException("inner"))));
            Exception ex = Assert.ThrowsAsync<PreflightBinaryCheckerException>(async () =>
                await _action.RecordAsync(_checker.CheckRemoteBinaryOnAttachAsync(_remoteTargetPid,
                    _target, _action)));
            Assert.IsInstanceOf<BinaryFileUtilException>(ex.InnerException);
            Assert.AreEqual(ErrorStrings.FailedToCheckRemoteBuildIdWithExplanation(
                ex.InnerException.Message), ex.Message);

            _metrics.Received().RecordEvent(
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
            _fileSystem.AddDirectory(_searchPaths.ElementAt(0));
            _fileSystem.AddFile(_localPaths[0], new MockFileData(""));
            _fileSystem.AddDirectory(_searchPaths.ElementAt(1));
            _fileSystem.AddFile(_localPaths[1], new MockFileData(""));

            // The modules are valid ELF files.
            _moduleParser.IsValidElf(Arg.Any<string>(), true, out string _).Returns(true);
            // Make the 2nd local file match the remote file, to force skipping the first file.
            _moduleParser.ParseRemoteBuildIdInfoAsync(_remoteTargetPath, _target)
                .Returns(Task.FromResult(_validBuildId));

            _moduleParser.ParseBuildIdInfo(_localPaths[0], ModuleFormat.Elf)
                .Returns(new BuildIdInfo() { Data = _validBuildId2 });

            _moduleParser.ParseBuildIdInfo(_localPaths[1], ModuleFormat.Elf)
                .Returns(new BuildIdInfo() { Data = _validBuildId });

            await _action.RecordAsync(_checker.CheckLocalAndRemoteBinaryOnLaunchAsync(
                _searchPaths, _executable, _target, _remoteTargetPaths, _action));

            _metrics.Received().RecordEvent(
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
            Exception ex = Assert.ThrowsAsync<PreflightBinaryCheckerException>(async () =>
                await _action.RecordAsync(_checker.CheckLocalAndRemoteBinaryOnLaunchAsync(
                _searchPaths, _executable, _target, _remoteTargetPaths, _action)));
            Assert.AreEqual(ErrorStrings.UnableToFindExecutable(_executable), ex.Message);

            _metrics.Received().RecordEvent(
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
            _fileSystem.AddDirectory(_searchPaths.ElementAt(0));
            _fileSystem.AddFile(_localPaths[0], new MockFileData(""));
            _moduleParser.ParseRemoteBuildIdInfoAsync(_remoteTargetPath, _target)
                .Returns(Task.FromException<BuildId>(new InvalidBuildIdException("test")));

            Exception ex = Assert.ThrowsAsync<PreflightBinaryCheckerException>(async () =>
                await _action.RecordAsync(_checker.CheckLocalAndRemoteBinaryOnLaunchAsync(
                    _searchPaths, _executable, _target, _remoteTargetPaths, _action)));
            Assert.IsInstanceOf<BinaryFileUtilException>(ex.InnerException);
            Assert.AreEqual(ErrorStrings.FailedToCheckRemoteBuildIdWithExplanation(
                ex.InnerException.Message), ex.Message);

            _metrics.Received().RecordEvent(
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
        public void CheckLocalAndRemoteBinaryFailsRemoteFileNotFound()
        {
            _moduleParser.ParseRemoteBuildIdInfoAsync(Arg.Any<string>(), _target)
                         .Throws(new BinaryFileUtilException("not found",
                                 new FileNotFoundException("not found")));

            Exception ex = Assert.ThrowsAsync<PreflightBinaryCheckerException>(async () =>
                await _action.RecordAsync(_checker.CheckLocalAndRemoteBinaryOnLaunchAsync(
                    _searchPaths, _executable, _target, _remoteTargetPaths, _action)));
            Assert.AreEqual(ErrorStrings.LaunchEndedGameBinaryNotFound, ex.Message);

            _metrics.Received().RecordEvent(
                DeveloperEventType.Types.Type.VsiDebugPreflightBinaryCheck,
                Arg.Is<DeveloperLogEvent>(m =>
                    m.StatusCode == DeveloperEventStatus.Types.Code.InvalidConfiguration &&
                    m.DebugPreflightCheckData.CheckType ==
                        DebugPreflightCheckData.Types.CheckType.RunAndAttach &&
                    m.DebugPreflightCheckData.RemoteBuildIdCheckResult ==
                        DebugPreflightCheckData.Types.RemoteBuildIdCheckResult
                            .RemoteBinaryError));
        }

        [Test]
        public void CheckLocalAndRemoteBinaryFailsReadLocal()
        {
            _fileSystem.AddDirectory(_searchPaths.ElementAt(0));
            _fileSystem.AddFile(_localPaths[0], new MockFileData(""));

            var buildIdInfoInvalid = new BuildIdInfo() {Data = new BuildId("BAAD")};
            _moduleParser
                .ParseBuildIdInfo(Arg.Any<string>(), ModuleFormat.Elf)
                .Returns(buildIdInfoInvalid);

            Exception ex = Assert.ThrowsAsync<PreflightBinaryCheckerException>(async () =>
                await _action.RecordAsync(_checker.CheckLocalAndRemoteBinaryOnLaunchAsync(
                    _searchPaths, _executable, _target, _remoteTargetPaths, _action)));
            Assert.AreEqual(ErrorStrings.UnableToFindExecutableMatchingRemoteBinary(_executable,
                _remoteTargetPath), ex.Message);


            // Note that the local build id failure results in an invalid configuration error
            // because we don't propagate the original BinaryFileUtilException but rather throw
            // our own ConfigurationException. (We may have failed on more than one file.)
            _metrics.Received().RecordEvent(
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
            _fileSystem.AddDirectory(_searchPaths.ElementAt(0));
            _fileSystem.AddFile(_localPaths[0], new MockFileData(""));

            var buildIdInfoInvalid = new BuildIdInfo();
            buildIdInfoInvalid.AddError("Not ELF");
            _moduleParser
                .ParseBuildIdInfo(Arg.Any<string>(), ModuleFormat.Elf)
                .Returns(buildIdInfoInvalid);

            Exception ex = Assert.ThrowsAsync<PreflightBinaryCheckerException>(async () =>
                await _action.RecordAsync(_checker.CheckLocalAndRemoteBinaryOnLaunchAsync(
                    _searchPaths, _executable, _target, _remoteTargetPaths, _action)));
            Assert.AreEqual(ErrorStrings.UnableToFindExecutableMatchingRemoteBinary(_executable,
                _remoteTargetPath), ex.Message);

            _metrics.Received().RecordEvent(
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
    }
}

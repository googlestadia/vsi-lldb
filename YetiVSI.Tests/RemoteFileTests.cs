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
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YetiCommon;
using YetiCommon.SSH;

namespace YetiVSI.Test
{
    [TestFixture]
    class RemoteFileTests
    {
        const string _ipAddress = "152.10.0.16";
        const string _localPath = @"d:\src\sample\bin\game.exe";
        const string _remotePath = @"/mnt/developer";

        readonly List<string> _ggpRsyncOutput = new List<string>
        {
            "1 file(s) and 0 folder(s) found",
            "1 file(s) and 0 folder(s) found ",
            "     1 file(s) and 0 folder(s) are not present on the gamelet and will be copied.",

            "   1% TOT 01:26 ETA",
            "   2% TOT 01:37 ETA",
            "   3% TOT 01:54 ETA",
            "   3% TOT 02:09 ETA",
            "  10% TOT 01:37 ETA",
            "  11% TOT 01:31 ETA",
            "  37% TOT 01:23 ETA",
            "  37% TOT 01:23 ETA"
        };

        ManagedProcess.Factory _managedProcessFactory;

        [SetUp]
        public void SetUp()
        {
            _managedProcessFactory = Substitute.For<ManagedProcess.Factory>();
        }

        [TestCase("edge/e-europe-west3-b/ab5679f0", 44722)]
        [TestCase("devkit/e-europe-west3-b/ab5679f0", 22)]
        public async Task SyncUsesCorrectArgumentsForDeployAlwaysAsync(string instanceId, int port)
        {
            bool force = true;
            var process = Substitute.For<IProcess>();
            _managedProcessFactory
                .Create(Arg.Is<ProcessStartInfo>(x => HasArgumentsAsExpected(x, port, force)),
                        Arg.Any<int>()).Returns(process);
            var remoteFile = new RemoteFile(_managedProcessFactory);
            var task = Substitute.For<ICancelable>();

            await remoteFile.SyncAsync(GetSshTarget(instanceId), _localPath, _remotePath, task,
                                       force);
            await process.Received(1).RunToExitWithSuccessAsync();
        }


        [TestCase("edge/e-europe-west3-b/ab5679f0", 44722)]
        [TestCase("devkit/e-europe-west3-b/ab5679f0", 22)]
        public async Task SyncUsesCorrectArgumentsForDeployDeltasAsync(string instanceId, int port)
        {
            bool force = false;
            var process = Substitute.For<IProcess>();
            _managedProcessFactory
                .Create(Arg.Is<ProcessStartInfo>(x => HasArgumentsAsExpected(x, port, force)),
                        Arg.Any<int>()).Returns(process);
            var remoteFile = new RemoteFile(_managedProcessFactory);
            var task = Substitute.For<ICancelable>();

            await remoteFile.SyncAsync(GetSshTarget(instanceId), _localPath, _remotePath, task,
                                       force);
            await process.Received(1).RunToExitWithSuccessAsync();
        }

        [Test]
        public async Task GetCallsCorrectBinaryAndOpensShellAsync()
        {
            var process = Substitute.For<IProcess>();
            _managedProcessFactory
                .CreateVisible(Arg.Is<ProcessStartInfo>(x => UsesScpWinExecutable(x)),
                               Arg.Any<int>()).Returns(process);
            var remoteFile = new RemoteFile(_managedProcessFactory);
            var task = Substitute.For<ICancelable>();

            await remoteFile.GetAsync(GetSshTarget(""), _localPath, _remotePath, task);
            await process.Received(1).RunToExitWithSuccessAsync();
        }

        [Test]
        public async Task SyncCallsCorrectBinaryAsync()
        {
            var process = Substitute.For<IProcess>();
            _managedProcessFactory
                .Create(Arg.Is<ProcessStartInfo>(x => UsesGgpRsync(x)), Arg.Any<int>())
                .Returns(process);
            var remoteFile = new RemoteFile(_managedProcessFactory);
            var task = Substitute.For<ICancelable>();

            await remoteFile.SyncAsync(GetSshTarget(""), _localPath, _remotePath, task);
            await process.Received(1).RunToExitWithSuccessAsync();
        }

        [Test]
        public void SyncThrowsWhenLocalPathNotPopulated([Values("", " ", null)] string localPath)
        {
            var remoteFile = new RemoteFile(_managedProcessFactory);
            var result = Assert.ThrowsAsync<ArgumentNullException>(
                async () => await remoteFile.SyncAsync(null, localPath, _remotePath, null));
            var message = "Local path should be specified when running ggp_rsync\r\n" +
                "Parameter name: localPath";
            Assert.That(result.Message, Is.EqualTo(message));
        }

        [Test]
        public void SyncThrowsWhenRemotePathNotPopulated([Values("", " ", null)] string remotePath)
        {
            var remoteFile = new RemoteFile(_managedProcessFactory);
            var result = Assert.ThrowsAsync<ArgumentNullException>(
                async () => await remoteFile.SyncAsync(null, _localPath, remotePath, null));
            var message = "Remote path should be specified when running ggp_rsync\r\n" +
                "Parameter name: remotePath";
            Assert.That(result.Message, Is.EqualTo(message));
        }

        [Test]
        public async Task ProcessOutputIsPropagatedIntoProgressDialogAsync()
        {
            var task = Substitute.For<ICancelable>();
            RemoteFile remoteFile = PrepareRemoteFileInstanceWithOutput(task, _ggpRsyncOutput);
            await remoteFile.SyncAsync(GetSshTarget(""), _localPath, _remotePath, task);

            var uniqueTrimmedOutputMessages = _ggpRsyncOutput.GroupBy(x => x.Trim())
                .ToDictionary(x => x.Key, x => x.Count());

            // Validate that the process output got propagated into _task.Progress.
            // The only processing is the message trimming (see above).
            Assert.Multiple(() =>
            {
                foreach (var keyValue in uniqueTrimmedOutputMessages)
                {
                    task.Progress.Received(keyValue.Value).Report(keyValue.Key);
                }
            });
        }

        [Test]
        public void ProcessOutputIsPropagatedIntoProgressDialogAndCancelledWhenRequested()
        {
            string singleLine = "Initial output from ggp rsync";
            var task = Substitute.For<ICancelable>();
            RemoteFile remoteFile =
                PrepareRemoteFileInstanceCanceledAfterFirstOutput(task, singleLine);

            // Validate that only one line is propagated into _task.Progress and after that
            // the OperationCanceledException is raised
            // (process raises event -> cancels the task -> raises event)
            Assert.Multiple(() =>
            {
                Assert.ThrowsAsync<OperationCanceledException>(
                    async () =>
                        await remoteFile.SyncAsync(GetSshTarget(""), _localPath, _remotePath,
                                                   task));
                task.Progress.Received(1).Report(singleLine);
            });
        }

        [Test]
        public void ProcessErrorCausesProcessException()
        {
            string errorMessage = "remote command returned exit code 128";
            var task = Substitute.For<ICancelable>();
            RemoteFile remoteFile = PrepareRemoteFileInstanceWithError(task, errorMessage);

            // Validate that only one line is propagated into _task.Progress and after that
            // the OperationCanceledException is raised (process raises event ->
            // cancels the task -> raises event)
            Assert.Multiple(() =>
            {
                var exception = Assert.ThrowsAsync<ProcessExecutionException>(
                    async () =>
                        await remoteFile.SyncAsync(GetSshTarget(""), _localPath, _remotePath,
                                                   task));
                StringAssert.Contains(errorMessage, exception.Message);
                task.Progress.Received(0).Report(Arg.Any<string>());
            });
        }

        /// <summary>
        /// Returns a RemoteFile, which calls the process sequentially raising
        /// OutputDataReceived events with the prepared content (output).
        /// </summary>
        /// <param name="task">Cancelable operation to associate with
        /// (responsible for the progressbar and cancellation handling).</param>
        /// <param name="output">Lines `produced` by the mock process.</param>
        /// <returns>Pre-configured RemoteFile.</returns>
        RemoteFile PrepareRemoteFileInstanceWithOutput(ICancelable task, List<string> output)
        {
            var process = new TestProcess(task, 0, output);
            _managedProcessFactory.Create(Arg.Any<ProcessStartInfo>(), Arg.Any<int>())
                .Returns(process);
            return new RemoteFile(_managedProcessFactory);
        }

        /// <summary>
        /// Returns a RemoteFile, which calls the process raising OutputDataReceived
        /// event once and sets the underlying task into `Canceled` state.
        /// </summary>
        /// <param name="task">Cancelable operation to associate with
        /// (responsible for the progressbar and cancellation handling).</param>
        /// <param name="singleLine">Line `produced` by the mock process before being
        /// cancelled.</param>
        /// <returns>Pre-configured RemoteFile.</returns>
        RemoteFile PrepareRemoteFileInstanceCanceledAfterFirstOutput(ICancelable task,
                                                                     string singleLine)
        {
            var process =
                new TestProcess(task, 0, new List<string>() { singleLine }, cancels: true);

            _managedProcessFactory.Create(Arg.Any<ProcessStartInfo>(), Arg.Any<int>())
                .Returns(process);
            return new RemoteFile(_managedProcessFactory);
        }

        /// <summary>
        /// Returns a RemoteFile, which calls the process raising ErrorDataReceived.
        /// </summary>
        /// <param name="task">Cancelable operation to associate with
        /// (responsible for the progressbar and cancellation handling).</param>
        /// <param name="error">Error message.</param>
        /// <returns>Pre-configured RemoteFile.</returns>
        RemoteFile PrepareRemoteFileInstanceWithError(ICancelable task, string error)
        {
            var process = new TestProcess(task, 1, errorMessage: error);
            _managedProcessFactory.Create(Arg.Any<ProcessStartInfo>(), Arg.Any<int>())
                .Returns(process);
            return new RemoteFile(_managedProcessFactory);
        }

        bool HasArgumentsAsExpected(ProcessStartInfo processStartInfo, int port, bool force) =>
            processStartInfo.Arguments.Equals(force
                                                  ? $"--port {port} --ip {_ipAddress} --compress --whole-file --checksum" +
                                                  $" \"{_localPath}\" \"{_remotePath}\""
                                                  : $"--port {port} --ip {_ipAddress} --compress " +
                                                  $" \"{_localPath}\" \"{_remotePath}\"");

        bool UsesGgpRsync(ProcessStartInfo processStartInfo) =>
            processStartInfo.FileName.EndsWith("ggp_rsync.exe");

        bool UsesScpWinExecutable(ProcessStartInfo processStartInfo) =>
            processStartInfo.FileName.EndsWith(YetiConstants.ScpWinExecutable);

        SshTarget GetSshTarget(string instanceId) =>
            new SshTarget(new Gamelet { Id = instanceId, IpAddr = _ipAddress });
    }

    public class TestProcess : IProcess
    {
        readonly ICancelable _cancelable;
        readonly int _exitCode;
        readonly List<string> _expectedOutput;
        readonly string _errorMessage;
        readonly bool _cancels;

        public TestProcess(ICancelable cancelable, int exitCode, List<string> expectedOutput = null,
                           string errorMessage = "", bool cancels = false)
        {
            _cancelable = cancelable;
            _exitCode = exitCode;
            _expectedOutput = expectedOutput;
            _errorMessage = errorMessage;
            _cancels = cancels;
        }

        public Task<int> RunToExitAsync()
        {
            if (_expectedOutput != null)
            {
                foreach (string message in _expectedOutput)
                {
                    OutputDataReceived?.Invoke(null, new TextReceivedEventArgs(message));
                    if (_cancels)
                    {
                        _cancelable.When(t => t.ThrowIfCancellationRequested())
                            .Throw<OperationCanceledException>();
                        break;
                    }
                }
            }

            if (_errorMessage != null)
            {
                ErrorDataReceived?.Invoke(null, new TextReceivedEventArgs(_errorMessage));
            }

            return Task.FromResult(_exitCode);
        }

        public void Dispose()
        {
            OnExit?.Invoke(null, new EventArgs());
        }

        public int Id { get; }
        public string ProcessName { get; }
        public int ExitCode { get; }
        public ProcessStartInfo StartInfo { get; }
        public event TextReceivedEventHandler OutputDataReceived;
        public event TextReceivedEventHandler ErrorDataReceived;
        public StreamReader StandardOutput { get; }
        public event EventHandler OnExit;

        public void Start(bool standardOutputReadLine = true)
        {
            throw new NotImplementedException();
        }

        public void Kill() => throw new NotImplementedException();

        public Task<int> WaitForExitAsync() => throw new NotImplementedException();

        public bool WaitForExit(TimeSpan timeout) => throw new NotImplementedException();
    }
}
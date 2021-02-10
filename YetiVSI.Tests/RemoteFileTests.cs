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
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using YetiCommon;
using YetiCommon.SSH;
using System.IO.Abstractions.TestingHelpers;

namespace YetiVSI.Test
{
    [TestFixture]
    class RemoteFileTests
    {
        const string _filePrefix = "YetiTransportSession";

        const string _testGameletId = "gameletid";
        const string _testGameletIP = "1.2.3.4";
        const string _testTargetDir = "C:\\testtargetdir";
        const string _testTargetFilename = "testtargetfilename";
        const string _testTargetPath = _testTargetDir + "\\" + _testTargetFilename;

        readonly string _testRemoteTargetPath = Path.Combine(YetiConstants.RemoteDeployPath,
                                                             _testTargetFilename);

        const string _testHash = "202cb962ac59075b964b07152d234b70";

        string[] _uncompressOutput = new string[]
        {
            "Listening on [0.0.0.0] (family 0, port 12345)",
            "Connection from [127.0.0.1] port 12345 [tcp/*] accepted (family 2, sport 55010),",
            _testHash + " *-"
        };

        string[] _uncompressOutputBadHash = new string[]
        {
            "Listening on [0.0.0.0] (family 0, port 12345)",
            "Connection from [127.0.0.1] port 12345 [tcp/*] accepted (family 2, sport 55010),",
            "badhash *-"
        };

        string[] _uncompressOutputNoListen = new string[]
        {
            "some error"
        };

        Gamelet _gamelet = new Gamelet {Id = _testGameletId, IpAddr = _testGameletIP};
        SshTarget _target;
        ManagedProcess.Factory _managedProcessFactory;
        RemoteFile _remoteFile;
        IProcess _compressProcess;
        IProcess _uncompressProcess;

        YetiVSI.DebugEngine.LldbTransportSession.Factory _transportSessionFactory;
        ILocalSocketSender _socketSender;
        MemoryMappedFileFactory _memoryMappedFileFactory;
        MemoryStream _compressedStream;
        IIncrementalProgress _progress;
        ICancelable _deployTask;
        readonly string _fileContent = "123";

        [SetUp]
        public void SetUp()
        {
            _compressedStream = new MemoryStream();
            StreamReader compressedOutput = new StreamReader(_compressedStream);

            _target = new SshTarget(_gamelet);
            _managedProcessFactory = Substitute.For<ManagedProcess.Factory>();
            _socketSender = Substitute.For<ILocalSocketSender>();
            _memoryMappedFileFactory = Substitute.For<MemoryMappedFileFactory>();
            _transportSessionFactory =
                new YetiVSI.DebugEngine.LldbTransportSession.Factory(_memoryMappedFileFactory);

            var fileSystem = new MockFileSystem();
            var fileData = new MockFileData(_fileContent);
            fileSystem.AddFile(_testTargetPath, fileData);

            _remoteFile = new RemoteFile(_managedProcessFactory, _transportSessionFactory,
                                         _socketSender, fileSystem);
            _compressProcess = Substitute.For<IProcess>();
            _uncompressProcess = Substitute.For<IProcess>();

            // Setup the uncompress process/ssh-tunnel.
            SetProcessOutput(_uncompressProcess, _uncompressOutput);
            _managedProcessFactory
                .Create(
                    Arg.Is<ProcessStartInfo>(
                        x => x.FileName.Contains(YetiConstants.SshWinExecutable)), int.MaxValue)
                .Returns(_uncompressProcess);

            // Setup the compress process.
            _compressProcess.StandardOutput.Returns(compressedOutput);
            _compressProcess.WaitForExitAsync().Returns(0);
            _managedProcessFactory
                .Create(
                    Arg.Is<ProcessStartInfo>(
                        x => x.FileName.Contains(YetiConstants.PigzExecutable)), int.MaxValue)
                .Returns(_compressProcess);

            _progress = Substitute.For<IIncrementalProgress>();
            _deployTask = Substitute.For<ICancelable>();
        }

        void SetProcessOutput(IProcess process, string[] output)
        {
            Action<NSubstitute.Core.CallInfo> sendEvents = x =>
            {
                foreach (var s in output)
                {
                    process.OutputDataReceived +=
                        Raise.Event<TextReceivedEventHandler>(this, new TextReceivedEventArgs(s));
                }
            };
            process.When(x => x.Start(true)).Do(sendEvents);
            process.When(x => x.RunToExitAsync()).Do(sendEvents);
        }

        [Test]
        public async Task PutUncompressedAsync()
        {
            var transferredDataSize = await _remoteFile.PutAsync(
                _target, _testTargetPath, _testRemoteTargetPath, DeployCompression.Uncompressed,
                _progress, _deployTask);

            Assert.AreEqual(_fileContent.Length, transferredDataSize);
        }

        [Test]
        public async Task PutUncompressedUncancellableAsync()
        {
            var transferredDataSize = await _remoteFile.PutAsync(
                _target, _testTargetPath, _testRemoteTargetPath, DeployCompression.Uncompressed,
                _progress, new NothingToCancel());

            Assert.AreEqual(_fileContent.Length, transferredDataSize);
        }

        [Test]
        public async Task PutCompressedCommandAsync()
        {
            // Check that we send the right command to the remote server.
            string sshArguments = "";

            const long compressedSize = 10;
            int expectedPort = WorkstationPorts.REMOTE_DEPLOY_AND_LLDB_GDB_SERVERS[0];

            _socketSender.SendAsync(Arg.Any<Stream>(), expectedPort, _progress, _deployTask)
                .Returns(compressedSize);

            // Record the arguments to ssh.
            _managedProcessFactory
                .Create(
                    Arg.Is<ProcessStartInfo>(
                        x => x.FileName.Contains(YetiConstants.SshWinExecutable)), int.MaxValue)
                .Returns(_uncompressProcess)
                .AndDoes(x => sshArguments = ((ProcessStartInfo) x[0]).Arguments);

            var transferredDataSize = await _remoteFile.PutAsync(
                _target, _testTargetPath, _testRemoteTargetPath, DeployCompression.Compressed,
                _progress, _deployTask);

            string expectedCommand = $"nc -vlp {expectedPort} | " + $"gzip -d --stdout | " +
                $"tee '{_testRemoteTargetPath}' | " + $"md5sum -b";
            StringAssert.EndsWith($"-- \"{expectedCommand}\"", sshArguments);

            Assert.AreEqual(compressedSize, transferredDataSize);
        }

        [Test]
        public async Task PutCompressedAsync()
        {
            // Check that the tunneled socket receives the compressed stream.

            await _remoteFile.PutAsync(_target, _testTargetPath, _testRemoteTargetPath,
                                       DeployCompression.Compressed, _progress, _deployTask);

            // Check the compressor received the right file name.
            _managedProcessFactory.Received()
                .Create(
                    Arg.Is<ProcessStartInfo>(
                        x => x.FileName.Contains(YetiConstants.PigzExecutable) &&
                            x.Arguments.Contains(_testTargetPath)), int.MaxValue);

            // Check that all processes were requested to finish.
            await _compressProcess.Received().WaitForExitAsync();
            await _uncompressProcess.Received().WaitForExitAsync();

            var port = WorkstationPorts.REMOTE_DEPLOY_AND_LLDB_GDB_SERVERS[0];
            // Check that socket received the compressed stream.
            await _socketSender.Received().SendAsync(Arg.Is(_compressedStream),
                                                     Arg.Is(port), Arg.Is(_progress),
                                                     Arg.Is(_deployTask));
        }

        [Test]
        public void PutCompressedHashMismatch()
        {
            // Check that file hash mismatch throws an exception.

            // Set the output sent by the uncompress process to contain a different hash.
            _uncompressOutputBadHash.CopyTo(_uncompressOutput, 0);

            Assert.ThrowsAsync<CompressedCopyException>(
                () => _remoteFile.PutAsync(_target, _testTargetPath, _testRemoteTargetPath,
                                           DeployCompression.Compressed, _progress, _deployTask));
        }

        [Test]
        public async Task PutCompressedHashUncompressDoesNotListenAsync()
        {
            // Check that after unexpected output from the uncompress process, the process is
            // killed (without waiting for process exit).

            _uncompressOutputNoListen.CopyTo(_uncompressOutput, 0);

            Assert.ThrowsAsync<CompressedCopyException>(
                () => _remoteFile.PutAsync(_target, _testTargetPath, _testRemoteTargetPath,
                                           DeployCompression.Compressed, _progress, _deployTask));

            await _uncompressProcess.DidNotReceiveWithAnyArgs().RunToExitAsync();
            await _uncompressProcess.DidNotReceiveWithAnyArgs().WaitForExitAsync();

            _uncompressProcess.Received().Kill();
        }

        [Test]
        public void PutCompressedAsyncFileHashThrows()
        {
            var mockFileSystem = Substitute.For<System.IO.Abstractions.IFileSystem>();
            var mockFile = Substitute.For<System.IO.Abstractions.IFileStreamFactory>();
            mockFileSystem.FileStream.Returns(mockFile);
            mockFile.Create(Arg.Any<string>(), FileMode.Open, FileAccess.Read).Returns(x => {
                throw new System.IO.IOException();
            });

            var remoteFile = new RemoteFile(_managedProcessFactory, _transportSessionFactory,
                                            _socketSender, mockFileSystem);

            // Check that the tunneled socket receives the compressed stream.
            Assert.ThrowsAsync<CompressedCopyException>(
                () => remoteFile.PutAsync(_target, _testTargetPath, _testRemoteTargetPath,
                                          DeployCompression.Compressed, _progress, _deployTask));
        }

        [Test]
        public async Task PutCompressedDifferentPortAsync()
        {
            // Check that we correctly choose the port for the transfer.

            _memoryMappedFileFactory.CreateNew(_filePrefix + 0, Arg.Any<long>())
                .Returns(x => { throw new System.IO.IOException(); });

            await _remoteFile.PutAsync(_target, _testTargetPath, _testRemoteTargetPath,
                                       DeployCompression.Compressed, _progress, _deployTask);

            var port = WorkstationPorts.REMOTE_DEPLOY_AND_LLDB_GDB_SERVERS[1];
            // Check that socket received the compressed stream.
            await _socketSender.Received().SendAsync(Arg.Is(_compressedStream),
                                                     Arg.Is(port), Arg.Is(_progress),
                                                     Arg.Is(_deployTask));
        }

        [Test]
        public void PutCompressedOutOfPorts()
        {
            // If we overflow the maximum number of sessions, we should fail.

            _memoryMappedFileFactory.CreateNew(Arg.Any<string>(), Arg.Any<long>())
                .Returns(x => { throw new System.IO.IOException(); });

            Assert.ThrowsAsync<CompressedCopyException>(
                () => _remoteFile.PutAsync(_target, _testTargetPath, _testRemoteTargetPath,
                                           DeployCompression.Compressed, _progress, _deployTask));
        }

        [Test]
        public void PutCompressedSocketError()
        {
            // Check that socket error during transfer causes the copying to fail.
            _socketSender
                .SendAsync(Arg.Is(_compressedStream), Arg.Any<int>(), Arg.Is(_progress),
                           Arg.Is(_deployTask))
                .Returns<Task<long>>(x => throw new System.Net.Sockets.SocketException());

            Assert.ThrowsAsync<CompressedCopyException>(
                () => _remoteFile.PutAsync(_target, _testTargetPath, _testRemoteTargetPath,
                                           DeployCompression.Compressed, _progress, _deployTask));
        }

        [Test]
        public void PutCompressedSshProcessFails()
        {
            // Check that a failure of the ssh process causes the copying to fail.

            _uncompressProcess.WaitForExitAsync().Returns(Task.FromResult(1));

            Assert.ThrowsAsync<CompressedCopyException>(
                () => _remoteFile.PutAsync(_target, _testTargetPath, _testRemoteTargetPath,
                                           DeployCompression.Compressed, _progress, _deployTask));
        }

        [Test]
        public void PutCompressedCompressProcessFails()
        {
            // Check that a failure of the compress process causes the copying to fail.

            _compressProcess.WaitForExitAsync().Returns(Task.FromResult(1));

            Assert.ThrowsAsync<ProcessExecutionException>(
                () => _remoteFile.PutAsync(_target, _testTargetPath, _testRemoteTargetPath,
                                           DeployCompression.Compressed, _progress, _deployTask));
        }
    }
}
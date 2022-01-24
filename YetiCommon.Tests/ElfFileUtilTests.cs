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

using NSubstitute;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TestsCommon.TestSupport;
using YetiCommon.SSH;

namespace YetiCommon.Tests
{
    [TestFixture]
    public class ElfFileUtilTests
    {
        static readonly string _fakeGameletIp = "127.0.0.1";
        static readonly string _fakeGameletPort = "1234";
        static readonly string _fakeFilename = @"C:\src\test.debug";
        static readonly string _fakeRemoteFilename = @"/mnt/developer/test.debug";

        ElfFileUtil _elfFileUtil;
        IProcess _mockProcess;
        IProcess _mockRemoteProcess;
        ManagedProcess.Factory _mockProcessFactory;
        LogSpy _logSpy;

        [SetUp]
        public void SetUp()
        {
            _mockProcess = Substitute.For<IProcess>();
            _mockProcess.ProcessName.Returns(YetiConstants.ObjDumpWinExecutable);
            _mockProcess.Id.Returns(1234);
            _mockProcessFactory = Substitute.For<ManagedProcess.Factory>();
            _mockProcessFactory
                .Create(Arg.Is<ProcessStartInfo>(
                    x => x.FileName.Contains(YetiConstants.ObjDumpWinExecutable) &&
                        x.Arguments.Contains(_fakeFilename)))
                .Returns(_mockProcess);

            _mockRemoteProcess = Substitute.For<IProcess>();
            _mockRemoteProcess.ProcessName.Returns(YetiConstants.SshWinExecutable);
            _mockRemoteProcess.Id.Returns(1234);
            _mockProcessFactory
                .Create(Arg.Is<ProcessStartInfo>(
                    x => x.FileName.Contains(YetiConstants.SshWinExecutable) &&
                        x.Arguments.Contains("cloudcast@" + _fakeGameletIp) &&
                        x.Arguments.Contains("-p " + _fakeGameletPort) &&
                        x.Arguments.Contains(YetiConstants.ObjDumpLinuxExecutable) &&
                        x.Arguments.Contains(_fakeRemoteFilename)))
                .Returns(_mockRemoteProcess);
            _elfFileUtil = new ElfFileUtil(_mockProcessFactory);

            _logSpy = new LogSpy();
            _logSpy.Attach();
        }

        [TearDown]
        public void TearDown()
        {
            _logSpy.Detach();
        }

        [TestCase("5D852C71-CC0C-0389-B7CD-7CECCF2FAE51-4FB3D058",
            new string[] {
                @"",
                @"C:\src\hello_ggp_c:      file format ELF64-x86-64",
                @"",
                @"Contents of section .note.gnu.build-id:",
                @" 201a40 04000000 14000000 03000000 474e5500  ............GNU.",
                @" 201a50 5d852c71 cc0c0389 b7cd7cec cf2fae51  ].,q......|../.Q",
                @" 201a60 4fb3d058 O..X",
                @"",
            })]
        [TestCase("01234567-89AB-CDEF", // ignoring non-hex parts
            new string[] {
                @"",
                @"C:\src\hello_ggp_c:      file format ELF64-x86-64",
                @"",
                @"Contents of section .note.gnu.build-id:",
                @" 201a40 04000000 14000000 03000000 474e5500  ............GNU.",
                @" 201a50 01234567 89abcdef ghijklmn opqrstuv  ................",
                @"",
        })]
        public async Task ReadBuildIdAsync(string buildIdStr, string[] outputLines)
        {
            _mockProcess.When(x => x.RunToExitAsync()).Do(x =>
            {
                OutputTestData(_mockProcess, outputLines);
            });

            var buildId = await _elfFileUtil.ReadBuildIdAsync(_fakeFilename);

            Assert.AreEqual(new BuildId(buildIdStr), buildId);
        }

        [TestCase("missing-build-id", new string[] {
                @"",
                @"C:\src\hello_ggp_c:      file format ELF64-x86-64",
                @"",
        })]
        [TestCase("empty-build-id", new string[] {
                @"",
                @"C:\src\hello_ggp_c:      file format ELF64-x86-64",
                @"",
                @"Contents of section .note.gnu.build-id:",
                @" 201a40 04000000 14000000 03000000 474e5500  ............GNU.",
                @"",
        })]
        [TestCase("odd-length-build-id", new string[] {
                @"",
                @"C:\src\hello_ggp_c:      file format ELF64-x86-64",
                @"",
                @"Contents of section .note.gnu.build-id:",
                @" 201a40 04000000 14000000 03000000 474e5500  ............GNU.",
                @" 201a50 5d852c71 cc0c0389 b7cd7cec cf2fae5   ].,q......|../.",
                @"",
        })]
        [TestCase("non-hex-build-id-is-empty", new string[] {
                @"",
                @"C:\src\hello_ggp_c:      file format ELF64-x86-64",
                @"",
                @"Contents of section .note.gnu.build-id:",
                @" 201a40 04000000 14000000 03000000 474e5500  ............GNU.",
                @" 201a50 hijklmno pqrstuvw                    ........",
                @"",
        })]
        public void ReadBuildId_InvalidBuildId(string descriptionUnused, string[] outputLines)
        {
            _mockProcess.When(x => x.RunToExitAsync()).Do(x =>
            {
                OutputTestData(_mockProcess, outputLines);
            });

            var ex = Assert.ThrowsAsync<InvalidBuildIdException>(
                () => _elfFileUtil.ReadBuildIdAsync(_fakeFilename));
            Assert.That(ex, Has.Message.Contain(_fakeFilename));
        }

        [Test]
        public void ReadBuildId_ProcessExecutionException()
        {
            var outputLines = new string[] { "output on error" };
            var errorLines = new string[] { "error on error" };
            _mockProcess.RunToExitAsync().Returns(1).AndDoes(x =>
            {
                OutputTestData(_mockProcess, outputLines, errorLines);
            });

            var ex = Assert.ThrowsAsync<BinaryFileUtilException>(
                () => _elfFileUtil.ReadBuildIdAsync(_fakeFilename));
            Assert.IsInstanceOf<ProcessExecutionException>(ex.InnerException);
            Assert.That(ex, Has.Message.Contain(_fakeFilename));

            Assert.That(_logSpy.GetOutput(), Contains.Substring(outputLines[0]));
            Assert.That(_logSpy.GetOutput(), Contains.Substring(errorLines[0]));
        }

        [Test]
        public void ReadBuildId_ProcessException()
        {
            _mockProcess.RunToExitAsync().Returns(
                Task.FromException<int>(new ProcessException("test")));

            var ex = Assert.ThrowsAsync<BinaryFileUtilException>(
                () => _elfFileUtil.ReadBuildIdAsync(_fakeFilename));
            Assert.IsInstanceOf<ProcessException>(ex.InnerException);
            // Note: message doesn't need to include filename.
        }

        [TestCase("5D852C71-CC0C-0389-B7CD-7CECCF2FAE51-4FB3D058",
            new string[] {
                @"",
                @"/mnt/developer/hello_ggp_c:     file format elf64-x86-64",
                @"",
                @"Contents of section .note.gnu.build-id:",
                @" 201a40 04000000 14000000 03000000 474e5500  ............GNU.",
                @" 201a50 5d852c71 cc0c0389 b7cd7cec cf2fae51  ].,q......|../.Q",
                @" 201a60 4fb3d058 O..X",
            })]
        public async Task ReadBuildId_RemoteAsync(string buildIdStr, string[] outputLines)
        {
            _mockRemoteProcess.When(x => x.RunToExitAsync()).Do(x =>
            {
                OutputTestData(_mockRemoteProcess, outputLines);
            });
            SshTarget target = new SshTarget(_fakeGameletIp + ":" + _fakeGameletPort);
            var buildId = await _elfFileUtil.ReadBuildIdAsync(_fakeRemoteFilename, target);
            Assert.AreEqual(new BuildId(buildIdStr), buildId);
        }

        [Test]
        public void ReadBuildId_Remote_ProcessExecutionException()
        {
            var outputLines = new string[] { "output on error" };
            var errorLines = new string[] { "error on error" };
            _mockRemoteProcess.RunToExitAsync().Returns(1).AndDoes(x =>
            {
                OutputTestData(_mockRemoteProcess, outputLines, errorLines);
            });
            SshTarget target = new SshTarget(_fakeGameletIp + ":" + _fakeGameletPort);

            var ex = Assert.ThrowsAsync<BinaryFileUtilException>(
                () => _elfFileUtil.ReadBuildIdAsync(_fakeRemoteFilename, target));
            Assert.IsInstanceOf<ProcessExecutionException>(ex.InnerException);
            Assert.That(ex, Has.Message.Contain(YetiConstants.ObjDumpLinuxExecutable));
            Assert.That(ex, Has.Message.Contain(_fakeRemoteFilename));

            Assert.That(_logSpy.GetOutput(), Contains.Substring(outputLines[0]));
            Assert.That(_logSpy.GetOutput(), Contains.Substring(errorLines[0]));
        }

        [Test]
        public void ReadBuildId_Remote_SshExecutionException()
        {
            var outputLines = new string[] { "output on error" };
            var errorLines = new string[] { "error on error" };
            _mockRemoteProcess.RunToExitAsync().Returns(255).AndDoes(x =>
            {
                OutputTestData(_mockRemoteProcess, outputLines, errorLines);
            });
            SshTarget target = new SshTarget(_fakeGameletIp + ":" + _fakeGameletPort);

            var ex = Assert.ThrowsAsync<BinaryFileUtilException>(
                () => _elfFileUtil.ReadBuildIdAsync(_fakeRemoteFilename, target));
            Assert.IsInstanceOf<ProcessExecutionException>(ex.InnerException);
            // Note: message doesn't need to include remote filename.

            Assert.That(_logSpy.GetOutput(), Contains.Substring(outputLines[0]));
            Assert.That(_logSpy.GetOutput(), Contains.Substring(errorLines[0]));
        }

        [Test]
        public void ReadBuildId_Remote_ProcessException()
        {
            _mockRemoteProcess.RunToExitAsync().Returns(
                Task.FromException<int>(new ProcessException("test")));
            SshTarget target = new SshTarget(_fakeGameletIp + ":" + _fakeGameletPort);

            var ex = Assert.ThrowsAsync<BinaryFileUtilException>(
                () => _elfFileUtil.ReadBuildIdAsync(_fakeRemoteFilename, target));
            Assert.IsInstanceOf<ProcessException>(ex.InnerException);
            // Note: message doesn't need to include remote filename.
        }

        void OutputTestData(IProcess process, string[] outputLines,
            string[] errorLines = null)
        {
            foreach (var line in outputLines)
            {
                process.OutputDataReceived +=
                    Raise.Event<TextReceivedEventHandler>(this, new TextReceivedEventArgs(line));
            }
            process.OutputDataReceived +=
                    Raise.Event<TextReceivedEventHandler>(this, new TextReceivedEventArgs(null));

            foreach (var line in errorLines ?? new string[] { })
            {
                process.ErrorDataReceived +=
                    Raise.Event<TextReceivedEventHandler>(this, new TextReceivedEventArgs(line));
            }
            process.ErrorDataReceived +=
                    Raise.Event<TextReceivedEventHandler>(this, new TextReceivedEventArgs(null));
        }
    }
}

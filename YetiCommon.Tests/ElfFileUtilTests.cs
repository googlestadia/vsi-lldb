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

        [Test]
        public void VerifySymbolFile_Success([Values(false, true)] bool isDebugInfoFile)
        {
            string output = @"
StadiaCppProject1.debug:        file format ELF64-x86-64

Sections:
Idx Name          Size     VMA          Type
  0               00000000 0000000000000000
  1 .interp       0000001c 00000000002002a8 BSS
  2 .note.ABI-tag 00000020 00000000002002c4
  3 .note.gnu.build-id 00000024 00000000002002e4
  4 .dynsym       00000108 0000000000200308 BSS
  5 .gnu.version  00000016 0000000000200410 BSS
  6 .gnu.version_r 00000020 0000000000200428 BSS
  7 .gnu.hash     00000028 0000000000200448 BSS
  8 .dynstr       000000fa 0000000000200470 BSS
  9 .rela.dyn     00000030 0000000000200570 BSS
 10 .rela.plt     00000030 00000000002005a0 BSS
 11 .rodata       0000000c 00000000002005d0 BSS
 12 .eh_frame_hdr 0000002c 00000000002005dc BSS
 13 .eh_frame     000000cc 0000000000200608 BSS
 14 .text         000001a2 0000000000201000 TEXT BSS
 15 .init         00000017 00000000002011a4 TEXT BSS
 16 .fini         00000009 00000000002011bc TEXT BSS
 17 .plt          00000030 00000000002011d0 TEXT BSS
 18 .jcr          00000008 0000000000202000 BSS
 19 .fini_array   00000008 0000000000202008 BSS
 20 .init_array   00000008 0000000000202010 BSS
 21 .dynamic      000001f0 0000000000202018 BSS
 22 .got          00000010 0000000000202208 BSS
 23 .bss.rel.ro   00000000 0000000000202218 BSS
 24 .data         00000010 0000000000203000 BSS
 25 .tm_clone_table 00000000 0000000000203010 BSS
 26 .got.plt      00000028 0000000000203010 BSS
 27 .bss          00000001 0000000000203038 BSS
 28 .comment      00000055 0000000000000000
 29 .debug_str    00000107 0000000000000000
 30 .debug_abbrev 00000064 0000000000000000
 31 .debug_info   0000009c 0000000000000000
 32 .debug_macinfo 00000001 0000000000000000
 33 .debug_names  0000012c 0000000000000000
 34 .debug_line   0000017d 0000000000000000
 35 .symtab       000006a8 0000000000000000
 36 .strtab       000001e5 0000000000000000
 37 .shstrtab     00000171 0000000000000000
";

            var outputLines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            var errorLines = Array.Empty<string>();
            _mockProcess.RunToExitAsync().Returns(0).AndDoes(
                x => { OutputTestData(_mockProcess, outputLines, errorLines); });

            Assert.DoesNotThrowAsync(
                () => _elfFileUtil.VerifySymbolFileAsync(_fakeFilename, isDebugInfoFile));
        }

        [Test]
        public async Task VerifySymbolFile_Executable_SuccessAsync()
        {
            string output = @"
abcd.debug:      file format ELF64-x86-64

Sections:
Idx Name          Size     VMA          Type
  0               00000000 0000000000000000
  1 .interp       0000001c 00000000002002a8 DATA
  2 .note.ABI-tag 00000020 00000000002002c4
  3 .note.gnu.build-id 00000024 00000000002002e4
  4 .dynsym       00000108 0000000000200308
  5 .gnu.version  00000016 0000000000200410
  6 .gnu.version_r 00000020 0000000000200428
  7 .gnu.hash     00000028 0000000000200448
  8 .dynstr       000000fa 0000000000200470
  9 .rela.dyn     00000030 0000000000200570
 10 .rela.plt     00000030 00000000002005a0
 11 .rodata       0000000c 00000000002005d0 DATA
 12 .eh_frame_hdr 0000002c 00000000002005dc DATA
 13 .eh_frame     000000cc 0000000000200608 DATA
 14 .text         000001a2 0000000000201000 TEXT
 15 .init         00000017 00000000002011a4 TEXT
 16 .fini         00000009 00000000002011bc TEXT
 17 .plt          00000030 00000000002011d0 TEXT
 18 .jcr          00000008 0000000000202000 DATA
 19 .fini_array   00000008 0000000000202008
 20 .init_array   00000008 0000000000202010
 21 .dynamic      000001f0 0000000000202018
 22 .got          00000010 0000000000202208 DATA
 23 .bss.rel.ro   00000000 0000000000202218 BSS
 24 .data         00000010 0000000000203000 DATA
 25 .tm_clone_table 00000000 0000000000203010 DATA
 26 .got.plt      00000028 0000000000203010 DATA
 27 .bss          00000001 0000000000203038 BSS
 28 .comment      00000055 0000000000000000
 29 .gnu_debuglink 0000001c 0000000000000000
 30 .shstrtab     00000123 0000000000000000
";

            var outputLines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            var errorLines = Array.Empty<string>();
            _mockProcess.RunToExitAsync().Returns(0).AndDoes(
                x => { OutputTestData(_mockProcess, outputLines, errorLines); });

            await _elfFileUtil.VerifySymbolFileAsync(_fakeFilename, false);
        }
        
        [Test]
        public void VerifySymbolFile_NoDebugInfo()
        {
            string output = @"
abcd.debug:      file format ELF64-x86-64

Sections:
Idx Name          Size     VMA          Type
  0               00000000 0000000000000000
  1 .interp       0000001c 00000000002002a8 DATA
  2 .note.ABI-tag 00000020 00000000002002c4
  3 .note.gnu.build-id 00000024 00000000002002e4
  4 .dynsym       00000108 0000000000200308
  5 .gnu.version  00000016 0000000000200410
  6 .gnu.version_r 00000020 0000000000200428
  7 .gnu.hash     00000028 0000000000200448
  8 .dynstr       000000fa 0000000000200470
  9 .rela.dyn     00000030 0000000000200570
 10 .rela.plt     00000030 00000000002005a0
 11 .rodata       0000000c 00000000002005d0 DATA
 12 .eh_frame_hdr 0000002c 00000000002005dc DATA
 13 .eh_frame     000000cc 0000000000200608 DATA
 14 .text         000001a2 0000000000201000 TEXT
 15 .init         00000017 00000000002011a4 TEXT
 16 .fini         00000009 00000000002011bc TEXT
 17 .plt          00000030 00000000002011d0 TEXT
 18 .jcr          00000008 0000000000202000 DATA
 19 .fini_array   00000008 0000000000202008
 20 .init_array   00000008 0000000000202010
 21 .dynamic      000001f0 0000000000202018
 22 .got          00000010 0000000000202208 DATA
 23 .bss.rel.ro   00000000 0000000000202218 BSS
 24 .data         00000010 0000000000203000 DATA
 25 .tm_clone_table 00000000 0000000000203010 DATA
 26 .got.plt      00000028 0000000000203010 DATA
 27 .bss          00000001 0000000000203038 BSS
 28 .comment      00000055 0000000000000000
 29 .gnu_debuglink 0000001c 0000000000000000
 30 .shstrtab     00000123 0000000000000000
";

            var outputLines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            var errorLines = Array.Empty<string>();
            _mockProcess.RunToExitAsync().Returns(0).AndDoes(
                x => { OutputTestData(_mockProcess, outputLines, errorLines); });

            var ex = Assert.ThrowsAsync<BinaryFileUtilException>(
                () => _elfFileUtil.VerifySymbolFileAsync(_fakeFilename, true));
            Assert.That(
                ex, Has.Message.Contain(ErrorStrings.MissingDebugInfoInSymbolFile(_fakeFilename)));
        }

        [Test]
        public void VerifySymbolFile_NoDebugInfo_ConfusingSuffix()
        {
            string output = @"
1234.debug_info :      file format ELF64-x86-64

Sections:
Idx Name          Size     VMA          Type
  0               00000000 0000000000000000
  1 .interp       0000001c 00000000002002a8 DATA
  2 .note.ABI-tag 00000020 00000000002002c4
  3 .note.gnu.build-id 00000024 00000000002002e4
  4 .dynsym       00000108 0000000000200308
  5 .gnu.version  00000016 0000000000200410
  6 .gnu.version_r 00000020 0000000000200428
  7 .gnu.hash     00000028 0000000000200448
  8 .dynstr       000000fa 0000000000200470
  9 .rela.dyn     00000030 0000000000200570
 10 .rela.plt     00000030 00000000002005a0
 11 .rodata       0000000c 00000000002005d0 DATA
 12 .eh_frame_hdr 0000002c 00000000002005dc DATA
 13 .eh_frame     000000cc 0000000000200608 DATA
 14 .text         000001a2 0000000000201000 TEXT
 15 .init         00000017 00000000002011a4 TEXT
 16 .fini         00000009 00000000002011bc TEXT
 17 .plt          00000030 00000000002011d0 TEXT
 18 .jcr          00000008 0000000000202000 DATA
 19 .fini_array   00000008 0000000000202008
 20 .init_array   00000008 0000000000202010
 21 .dynamic      000001f0 0000000000202018
 22 .got          00000010 0000000000202208 DATA
 23 .bss.rel.ro   00000000 0000000000202218 BSS
 24 .data         00000010 0000000000203000 DATA
 25 .tm_clone_table 00000000 0000000000203010 DATA
 26 .got.plt      00000028 0000000000203010 DATA
 27 .bss          00000001 0000000000203038 BSS
 28 .comment      00000055 0000000000000000
 29 .gnu_debuglink 0000001c 0000000000000000
 30 .shstrtab     00000123 0000000000000000
";

            var outputLines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            var errorLines = Array.Empty<string>();
            _mockProcess.RunToExitAsync().Returns(0).AndDoes(
                x => { OutputTestData(_mockProcess, outputLines, errorLines); });

            var ex = Assert.ThrowsAsync<BinaryFileUtilException>(
                () => _elfFileUtil.VerifySymbolFileAsync(_fakeFilename, true));
            Assert.That(
                ex, Has.Message.Contain(ErrorStrings.MissingDebugInfoInSymbolFile(_fakeFilename)));
        }

        [Test]
        public void VerifySymbolFile_Truncated([Values(false, true)] bool isDebugInfoFile)
        {
            string errorOutput =
                @"C:\Program Files\GGP SDK\BaseSDK\LLVM\9.0.1\bin\llvm-objdump.exe: " +
                "error: 'abcd.debug': section table goes past the end of file";

            var outputLines = Array.Empty<string>();
            var errorLines = new[] { errorOutput };
            _mockProcess.RunToExitAsync().Returns(1).AndDoes(
                x => { OutputTestData(_mockProcess, outputLines, errorLines); });

            var ex = Assert.ThrowsAsync<BinaryFileUtilException>(
                () => _elfFileUtil.VerifySymbolFileAsync(_fakeFilename, isDebugInfoFile));
            Assert.That(ex, Has.Message.Contain(ErrorStrings.SymbolFileTruncated(_fakeFilename)));
        }

        [Test]
        public void VerifySymbolFile_Invalid([Values(false, true)] bool isDebugInfoFile)
        {
            string errorOutput =
                @"C:\Program Files\GGP SDK\BaseSDK\LLVM\9.0.1\bin\llvm-objdump.exe: " +
                "error: 'abcd.debug': The file was not recognized as a valid object file";

            var outputLines = Array.Empty<string>();
            var errorLines = new string[] { errorOutput };
            _mockProcess.RunToExitAsync().Returns(1).AndDoes(
                x => { OutputTestData(_mockProcess, outputLines, errorLines); });

            var ex = Assert.ThrowsAsync<BinaryFileUtilException>(
                () => _elfFileUtil.VerifySymbolFileAsync(_fakeFilename, isDebugInfoFile));
            Assert.That(ex,
                        Has.Message.Contain(ErrorStrings.InvalidSymbolFileFormat(_fakeFilename)));
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

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

using System.Collections.Generic;
using System.IO;
using DebuggerApi;
using DebuggerCommonApi;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using NSubstitute;
using NUnit.Framework;
using YetiVSI.DebugEngine;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class DebugDisassemblyStreamTests
    {
        const string _testMnemonic = "Test Mnemonic";
        const string _testOperands = "Test Operands";
        const string _testComment = "Test Comment";
        const ulong _testAddress = 0x123456789abc;
        const string _testDirectory = "C:\\";
        const string _testFilename = "main.cc";
        const enum_DISASSEMBLY_STREAM_SCOPE _testScope = enum_DISASSEMBLY_STREAM_SCOPE.DSS_ALL;

        DebugCodeContext.Factory _codeContextFactory;
        DebugDocumentContext.Factory _documentContextFactory;
        IGgpDebugCodeContext _mockCodeContext;
        RemoteTarget _mockTarget;

        IDebugDisassemblyStream2 _disassemblyStream;
        readonly string _flavor = "intel";

        [SetUp]
        public void SetUp()
        {
            _codeContextFactory = Substitute.For<DebugCodeContext.Factory>();
            _documentContextFactory = Substitute.For<DebugDocumentContext.Factory>();

            _mockTarget = Substitute.For<RemoteTarget>();
            _mockCodeContext = Substitute.For<IGgpDebugCodeContext>();
            _mockCodeContext.Address.Returns(_testAddress);

            _disassemblyStream = new DebugDisassemblyStream.Factory(
                _codeContextFactory, _documentContextFactory).Create(
                _testScope, _mockCodeContext, _mockTarget);
        }

        [Test]
        public void GetCodeContext()
        {
            const ulong newAddress = 0x123456789a;

            SbAddress address = Substitute.For<SbAddress>();
            var documentContext = Substitute.For<IDebugDocumentContext2>();
            var newCodeContext = Substitute.For<IGgpDebugCodeContext>();

            _mockTarget.ResolveLoadAddress(newAddress).Returns(address);
            _documentContextFactory.Create(address.GetLineEntry()).Returns(documentContext);
            _codeContextFactory
                .Create(_mockTarget, newAddress, null, documentContext)
                .Returns(newCodeContext);

            Assert.AreEqual(
                VSConstants.S_OK,
                _disassemblyStream.GetCodeContext(newAddress, out IDebugCodeContext2 codeContext));
            Assert.AreEqual(newCodeContext, codeContext);
        }

        [Test]
        public void GetCodeContextFunctionNameResolution()
        {
            _disassemblyStream = new DebugDisassemblyStream.Factory(
                new DebugCodeContext.Factory(), _documentContextFactory).Create(
                _testScope, _mockCodeContext, _mockTarget);

            const ulong newAddress = 0x123456789a;

            var address = Substitute.For<SbAddress>();
            address.GetFunction().GetName().Returns("funcName()");
            _mockTarget.ResolveLoadAddress(newAddress).Returns(address);

            Assert.That(
                _disassemblyStream.GetCodeContext(newAddress, out IDebugCodeContext2 codeContext),
                Is.EqualTo(VSConstants.S_OK));

            Assert.That(codeContext.GetName(out string name), Is.EqualTo(VSConstants.S_OK));
            Assert.That(name, Is.EqualTo("funcName()"));
        }

        [Test]
        public void GetCodeContextNullAddressResolution()
        {
            SbAddress address = null;
            var newCodeContext = Substitute.For<IGgpDebugCodeContext>();

            _mockTarget.ResolveLoadAddress(_testAddress).Returns(address);
            _codeContextFactory
                .Create(_mockTarget, _testAddress, null, null)
                .Returns(newCodeContext);

            int getCodeContextResult =
                _disassemblyStream.GetCodeContext(_testAddress, out IDebugCodeContext2 codeContext);
            Assert.AreEqual(VSConstants.S_OK, getCodeContextResult);
            Assert.AreEqual(newCodeContext, codeContext);
        }

        [Test]
        public void GetCodeLocationId()
        {
            const ulong newAddress = 0x123456789a;

            var newCodeContext = Substitute.For<IGgpDebugCodeContext>();
            newCodeContext.Address.Returns(newAddress);

            Assert.AreEqual(VSConstants.S_OK, _disassemblyStream.GetCodeLocationId(
                                                  newCodeContext, out ulong codeLocationId));
            Assert.AreEqual(newAddress, codeLocationId);
        }

        [Test]
        public void GetCurrentLocation()
        {
            Assert.AreEqual(VSConstants.S_OK,
                            _disassemblyStream.GetCurrentLocation(out ulong codeLocationId));
            Assert.AreEqual(_testAddress, codeLocationId);
        }

        [Test]
        public void GetScope()
        {
            var scope = new enum_DISASSEMBLY_STREAM_SCOPE[1];
            Assert.AreEqual(VSConstants.S_OK, _disassemblyStream.GetScope(scope));
            Assert.AreEqual(_testScope, scope[0]);
        }

        [Test]
        public void GetSize()
        {
            Assert.AreEqual(VSConstants.S_OK, _disassemblyStream.GetSize(out ulong size));
            Assert.AreEqual(0xFFFFFFFF, size);
        }

        [Test]
        public void ReadOneInstructionWithoutSource()
        {
            uint numberInstructionsToCreate = 1;
            uint numberInstructionsToRead = 10;
            MockRead(numberInstructionsToCreate, numberInstructionsToRead);

            var disassembly = new DisassemblyData[numberInstructionsToRead];
            Assert.AreEqual(VSConstants.S_OK,
                            _disassemblyStream.Read((uint)disassembly.Length,
                                                    enum_DISASSEMBLY_STREAM_FIELDS.DSF_ALL,
                                                    out uint numInstructionsRead, disassembly));
            Assert.AreEqual(1, numInstructionsRead);
            Assert.AreEqual(enum_DISASSEMBLY_STREAM_FIELDS.DSF_ADDRESS |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_CODELOCATIONID |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_OPCODE |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_DOCUMENTURL |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_FLAGS,
                            disassembly[0].dwFields);
            Assert.AreEqual(_testAddress, disassembly[0].uCodeLocationId);
            Assert.AreEqual("0x0000123456789abc", disassembly[0].bstrAddress);
            Assert.AreEqual(
                $"{_testMnemonic + 0} {_testOperands + 0}                 # {_testComment}",
                disassembly[0].bstrOpcode);
            Assert.AreEqual((enum_DISASSEMBLY_FLAGS)0, disassembly[0].dwFlags);
        }

        [Test]
        public void ReadOneInstructionWithSource()
        {
            uint numberInstructionsToCreate = 1;
            uint numberInstructionsToRead = 10;
            var mockInstructions = MockRead(numberInstructionsToCreate, numberInstructionsToRead);

            var lineEntry = CreateLineEntry(_testFilename, _testDirectory, 10u, 0u);
            mockInstructions[0].LineEntry = lineEntry;

            var disassembly = new DisassemblyData[numberInstructionsToRead];
            Assert.AreEqual(VSConstants.S_OK,
                            _disassemblyStream.Read((uint)disassembly.Length,
                                                    enum_DISASSEMBLY_STREAM_FIELDS.DSF_ALL,
                                                    out uint numberInstructionsRead, disassembly));
            Assert.AreEqual(1, numberInstructionsRead);
            Assert.AreEqual(enum_DISASSEMBLY_STREAM_FIELDS.DSF_ADDRESS |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_CODELOCATIONID |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_OPCODE |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_DOCUMENTURL |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_POSITION |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_BYTEOFFSET |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_FLAGS,
                            disassembly[0].dwFields);
            Assert.AreEqual(FormatUrl(_testDirectory, _testFilename),
                            disassembly[0].bstrDocumentUrl);
            Assert.AreEqual(9u, disassembly[0].posEnd.dwLine);
            Assert.AreEqual(0u, disassembly[0].posBeg.dwLine);
            Assert.AreEqual(enum_DISASSEMBLY_FLAGS.DF_HASSOURCE |
                                enum_DISASSEMBLY_FLAGS.DF_DOCUMENTCHANGE,
                            disassembly[0].dwFlags);
        }

        [Test]
        public void ReadOneInstructionWithSourceSameFile()
        {
            uint numberInstructionsToCreate = 1;
            uint numberInstructionsToRead = 10;
            uint previousLine = 6u;
            uint endLine = 9u;
            var mockInstructions = MockRead(numberInstructionsToCreate, numberInstructionsToRead);

            var lineEntry = CreateLineEntry(_testFilename, _testDirectory, endLine + 1u, 0u);
            mockInstructions[0].LineEntry = lineEntry;

            var previousEntry =
                new LineEntryInfo
                {
                    Line = previousLine + 1u,
                    Directory = _testDirectory,
                    FileName = _testFilename
                };
            _mockTarget.ResolveLoadAddress(_testAddress - 1).GetLineEntry().Returns(previousEntry);

            var disassembly = new DisassemblyData[numberInstructionsToRead];
            Assert.AreEqual(VSConstants.S_OK,
                            _disassemblyStream.Read((uint)disassembly.Length,
                                                    enum_DISASSEMBLY_STREAM_FIELDS.DSF_ALL,
                                                    out uint numberInstructionsRead, disassembly));
            Assert.AreEqual(1, numberInstructionsRead);
            Assert.AreEqual(enum_DISASSEMBLY_STREAM_FIELDS.DSF_ADDRESS |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_CODELOCATIONID |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_OPCODE |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_DOCUMENTURL |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_POSITION |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_BYTEOFFSET |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_FLAGS,
                            disassembly[0].dwFields);
            Assert.AreEqual(FormatUrl(_testDirectory, _testFilename),
                            disassembly[0].bstrDocumentUrl);
            Assert.AreEqual(previousLine + 1, disassembly[0].posBeg.dwLine);
            Assert.AreEqual(0, disassembly[0].dwByteOffset);
            Assert.AreEqual(enum_DISASSEMBLY_FLAGS.DF_HASSOURCE, disassembly[0].dwFlags);
        }

        [Test]
        public void ReadTwoInstructionsWithSourceSameFile()
        {
            uint numberInstructionsToCreate = 2;
            uint numberInstructionsToRead = 10;
            uint firstLine = 6u;
            uint secondLine = 9u;
            var mockInstructions = MockRead(numberInstructionsToCreate, numberInstructionsToRead);

            var firstLineEntry = CreateLineEntry(_testFilename, _testDirectory, firstLine + 1u, 0u);
            mockInstructions[0].LineEntry = firstLineEntry;

            var secondLineEntry =
                CreateLineEntry(_testFilename, _testDirectory, secondLine + 1u, 0u);
            mockInstructions[1].LineEntry = secondLineEntry;

            var disassembly = new DisassemblyData[numberInstructionsToRead];
            Assert.AreEqual(VSConstants.S_OK,
                            _disassemblyStream.Read((uint)disassembly.Length,
                                                    enum_DISASSEMBLY_STREAM_FIELDS.DSF_ALL,
                                                    out uint numInstructionsRead, disassembly));
            Assert.AreEqual(2, numInstructionsRead);

            Assert.AreEqual(enum_DISASSEMBLY_STREAM_FIELDS.DSF_ADDRESS |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_CODELOCATIONID |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_OPCODE |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_DOCUMENTURL |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_POSITION |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_BYTEOFFSET |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_FLAGS,
                            disassembly[1].dwFields);
            Assert.AreEqual(null, disassembly[1].bstrDocumentUrl);
            Assert.AreEqual(firstLine + 1, disassembly[1].posBeg.dwLine);
            Assert.AreEqual(secondLine, disassembly[1].posEnd.dwLine);
            Assert.AreEqual(0, disassembly[1].dwByteOffset);
            Assert.AreEqual(enum_DISASSEMBLY_FLAGS.DF_HASSOURCE, disassembly[1].dwFlags);
        }

        [Test]
        public void ReadTwoInstructionsWithSourceSameLine()
        {
            uint numberInstructionsToCreate = 2;
            uint numberInstructionsToRead = 10;
            uint firstLine = 6u;
            var mockInstructions = MockRead(numberInstructionsToCreate, numberInstructionsToRead);

            var lineEntry = CreateLineEntry(_testFilename, _testDirectory, firstLine + 1u, 0u);
            mockInstructions[0].LineEntry = lineEntry;
            mockInstructions[1].LineEntry = lineEntry;

            var disassembly = new DisassemblyData[numberInstructionsToRead];
            Assert.AreEqual(VSConstants.S_OK,
                            _disassemblyStream.Read((uint)disassembly.Length,
                                                    enum_DISASSEMBLY_STREAM_FIELDS.DSF_ALL,
                                                    out uint numInstructionsRead, disassembly));
            Assert.AreEqual(2, numInstructionsRead);

            Assert.AreEqual(enum_DISASSEMBLY_STREAM_FIELDS.DSF_ADDRESS |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_CODELOCATIONID |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_OPCODE |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_DOCUMENTURL |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_POSITION |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_BYTEOFFSET |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_FLAGS,
                            disassembly[1].dwFields);
            // If the instruction are not on the same line, then the offset from the beginning
            // of the line must not be zero. VS does not seem to care about the precise value,
            // it only seems to care about the zero vs. non-zero distinction.
            Assert.AreNotEqual(0, disassembly[1].dwByteOffset);
            Assert.AreEqual(enum_DISASSEMBLY_FLAGS.DF_HASSOURCE, disassembly[1].dwFlags);
        }

        [Test]
        public void ReadTwoInstructionsWithSourceDifferentFile()
        {
            uint numberInstructionsToCreate = 2;
            uint numberInstructionsToRead = 10;
            string otherFilename = "other.cc";
            uint line = 5u;
            var mockInstructions = MockRead(numberInstructionsToCreate, numberInstructionsToRead);

            var firstLineEntry = CreateLineEntry(_testFilename, _testDirectory, line + 1u, 0u);
            mockInstructions[0].LineEntry = firstLineEntry;

            var secondLineEntry = CreateLineEntry(otherFilename, _testDirectory, line + 1u, 0u);
            mockInstructions[1].LineEntry = secondLineEntry;

            var disassembly = new DisassemblyData[numberInstructionsToRead];
            Assert.AreEqual(VSConstants.S_OK,
                            _disassemblyStream.Read((uint)disassembly.Length,
                                                    enum_DISASSEMBLY_STREAM_FIELDS.DSF_ALL,
                                                    out uint numInstructionsRead, disassembly));
            Assert.AreEqual(2, numInstructionsRead);

            Assert.AreEqual(enum_DISASSEMBLY_STREAM_FIELDS.DSF_ADDRESS |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_CODELOCATIONID |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_OPCODE |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_DOCUMENTURL |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_POSITION |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_BYTEOFFSET |
                                enum_DISASSEMBLY_STREAM_FIELDS.DSF_FLAGS,
                            disassembly[1].dwFields);
            Assert.AreEqual(FormatUrl(_testDirectory, otherFilename),
                            disassembly[1].bstrDocumentUrl);
            Assert.AreEqual(0u, disassembly[1].posBeg.dwLine);
            Assert.AreEqual(line, disassembly[1].posEnd.dwLine);
            Assert.AreEqual(0, disassembly[1].dwByteOffset);
            Assert.AreEqual(enum_DISASSEMBLY_FLAGS.DF_HASSOURCE |
                                enum_DISASSEMBLY_FLAGS.DF_DOCUMENTCHANGE,
                            disassembly[1].dwFlags);
        }

        [Test]
        public void ReadFull()
        {
            uint numberInstructions = 20;
            MockRead(numberInstructions, numberInstructions);

            var disassembly = new DisassemblyData[numberInstructions];
            Assert.AreEqual(VSConstants.S_OK,
                            _disassemblyStream.Read((uint)disassembly.Length,
                                                    enum_DISASSEMBLY_STREAM_FIELDS.DSF_ALL,
                                                    out uint numInstructionsRead, disassembly));
            Assert.AreEqual(20, numInstructionsRead);
        }

        [Test]
        public void ReadEmpty()
        {
            uint instructionsToRead = 10;
            MockRead(0, instructionsToRead);

            var disassembly = new DisassemblyData[instructionsToRead];
            Assert.AreEqual(VSConstants.S_FALSE,
                            _disassemblyStream.Read((uint)disassembly.Length,
                                                    enum_DISASSEMBLY_STREAM_FIELDS.DSF_ALL,
                                                    out uint numInstructionsRead, disassembly));
            Assert.AreEqual(0, numInstructionsRead);
        }

        [Test]
        public void SeekContext()
        {
            const ulong newAddress = 0x0001;
            var newCodeContext = Substitute.For<IGgpDebugCodeContext>();
            newCodeContext.Address.Returns(newAddress);

            Assert.AreEqual(VSConstants.S_OK,
                            _disassemblyStream.Seek(enum_SEEK_START.SEEK_START_CODECONTEXT,
                                                    newCodeContext, 0, 0));
            _disassemblyStream.GetCurrentLocation(out ulong newLocation);
            Assert.AreEqual(newAddress, newLocation);
        }

        [Test]
        public void SeekLocation()
        {
            const ulong newAddress = 0x0001;

            Assert.AreEqual(
                VSConstants.S_OK,
                _disassemblyStream.Seek(enum_SEEK_START.SEEK_START_CODELOCID, null, newAddress, 0));

            _disassemblyStream.GetCurrentLocation(out ulong newLocation);
            Assert.AreEqual(newAddress, newLocation);
        }

        [Test]
        public void SeekPositive()
        {
            uint numberInstructions = 20;
            AddInstructions(numberInstructions);

            Assert.AreEqual(VSConstants.S_OK,
                            _disassemblyStream.Seek(enum_SEEK_START.SEEK_START_CURRENT, null, 0,
                                                    numberInstructions));

            _disassemblyStream.GetCurrentLocation(out ulong newLocation);
            Assert.AreEqual(_testAddress + numberInstructions, newLocation);
        }

        [Test]
        public void SeekNegativeInvalidStartInstruction()
        {
            var mockAddress = Substitute.For<SbAddress>();
            _mockTarget.ResolveLoadAddress(_testAddress).Returns(mockAddress);
            _mockTarget.ReadInstructionInfos(mockAddress, 1, _flavor)
                .Returns(new List<InstructionInfo>());
            Assert.AreEqual(
                VSConstants.E_FAIL,
                _disassemblyStream.Seek(enum_SEEK_START.SEEK_START_CURRENT, null, 0, -10));

            // Location shouldn't change.
            _disassemblyStream.GetCurrentLocation(out ulong newLocation);
            Assert.AreEqual(_testAddress, newLocation);
        }

        [Test]
        public void SeekNegativeInstructionBoundary()
        {
            uint instructionsToSeek = 10;
            uint maxInstructionsInRange = 150;
            MockStartInstruction();

            // Add instructions starting right at the seek address.
            AddInstructions(maxInstructionsInRange, _testAddress - maxInstructionsInRange);

            Assert.AreEqual(VSConstants.S_OK,
                            _disassemblyStream.Seek(enum_SEEK_START.SEEK_START_CURRENT, null, 0,
                                                    -instructionsToSeek));

            _disassemblyStream.GetCurrentLocation(out ulong newLocation);
            Assert.AreEqual(_testAddress - instructionsToSeek, newLocation);
        }

        [Test]
        public void SeekNegativeMiddleInstruction()
        {
            uint instructionsToSeek = 10;
            uint maxInstructionsInRange = 150;
            MockStartInstruction();

            // Return an empty list for the first 3 read attempts. This simulates bad / invalid
            // instructions at those addresses.
            for (uint i = 0; i < 3; i++)
            {
                var mockAddress = Substitute.For<SbAddress>();
                _mockTarget.ResolveLoadAddress(_testAddress - maxInstructionsInRange + i)
                    .Returns(mockAddress);
                _mockTarget.ReadInstructionInfos(mockAddress, maxInstructionsInRange + 1, _flavor)
                    .Returns(new List<InstructionInfo>());
            }

            // Add instructions starting at 3 bytes past the seek address.
            AddInstructions(maxInstructionsInRange, _testAddress - maxInstructionsInRange + 3);

            Assert.AreEqual(VSConstants.S_OK,
                            _disassemblyStream.Seek(enum_SEEK_START.SEEK_START_CURRENT, null, 0,
                                                    -instructionsToSeek));

            _disassemblyStream.GetCurrentLocation(out ulong newLocation);
            Assert.AreEqual(_testAddress - instructionsToSeek, newLocation);
        }

        [Test]
        public void SeekNegativeBeginningLongInstruction()
        {
            uint instructionsToSeek = 10;
            uint maxInstructionsInRange = 150;
            MockStartInstruction();

            // Return an empty list for the first 13 read attempts. This simulates bad / invalid
            // instructions at those addresses.
            for (uint i = 0; i < 14; i++)
            {
                var mockAddress = Substitute.For<SbAddress>();
                _mockTarget.ResolveLoadAddress(_testAddress - maxInstructionsInRange + i)
                    .Returns(mockAddress);
                _mockTarget.ReadInstructionInfos(mockAddress, maxInstructionsInRange + 1, _flavor)
                    .Returns(new List<InstructionInfo>());
            }

            // Add instructions starting at 14 bytes past the seek address.
            AddInstructions(150, _testAddress - maxInstructionsInRange + 14);

            Assert.AreEqual(VSConstants.S_OK,
                            _disassemblyStream.Seek(enum_SEEK_START.SEEK_START_CURRENT, null, 0,
                                                    -instructionsToSeek));

            _disassemblyStream.GetCurrentLocation(out ulong newLocation);
            Assert.AreEqual(_testAddress - instructionsToSeek, newLocation);
        }

        [Test]
        public void SeekNegativeInvalidSeekAddress()
        {
            uint instructionsToSeek = 10;
            uint maxInstructionsInRange = 150;
            MockStartInstruction();

            // Return an empty list for all read attempts.
            for (uint i = 0; i < 15; i++)
            {
                var mockAddress = Substitute.For<SbAddress>();
                _mockTarget.ResolveLoadAddress(_testAddress - maxInstructionsInRange + i)
                    .Returns(mockAddress);
                _mockTarget.ReadInstructionInfos(mockAddress, maxInstructionsInRange + 1, _flavor)
                    .Returns(new List<InstructionInfo>());
            }

            // The seek should fail as there are no valid instructions.
            Assert.AreEqual(VSConstants.E_FAIL,
                            _disassemblyStream.Seek(enum_SEEK_START.SEEK_START_CURRENT, null, 0,
                                                    -instructionsToSeek));

            // Location shouldn't change.
            _disassemblyStream.GetCurrentLocation(out ulong newLocation);
            Assert.AreEqual(_testAddress, newLocation);
        }

        [Test]
        public void SeekNegativeNoStartInList()
        {
            uint instructionsToSeek = 10;
            uint maxInstructionsInRange = 150;

            // Setup start instruction that won't exist in the list of instructions.
            var startInstruction = new InstructionInfo();
            var mockStartAddress = Substitute.For<SbAddress>();
            // mockStartInstruction.GetAddress().Returns(mockStartAddress);
            startInstruction.Address = 0u;
            mockStartAddress.GetLoadAddress(_mockTarget).Returns((uint)0);
            _mockTarget.ResolveLoadAddress(_testAddress).Returns(mockStartAddress);
            _mockTarget.ReadInstructionInfos(mockStartAddress, 1, _flavor)
                .Returns(new List<InstructionInfo> { startInstruction });

            // Add instructions starting right at the seek address.
            AddInstructions(maxInstructionsInRange, _testAddress - maxInstructionsInRange);

            Assert.AreEqual(VSConstants.E_FAIL,
                            _disassemblyStream.Seek(enum_SEEK_START.SEEK_START_CURRENT, null, 0,
                                                    -instructionsToSeek));

            // Location shouldn't change.
            _disassemblyStream.GetCurrentLocation(out ulong newLocation);
            Assert.AreEqual(_testAddress, newLocation);
        }

        [Test]
        public void SeekNegativeIndexOutOfBounds()
        {
            uint instructionsToSeek = 10;
            uint maxInstructionsInRange = 150;
            MockStartInstruction();

            // Create instructions where the start address + numInstructions will be out of bounds.
            var instructions = CreateMockInstructions(maxInstructionsInRange + 1, _testAddress);
            var mockAddress = Substitute.For<SbAddress>();
            _mockTarget.ResolveLoadAddress(_testAddress - maxInstructionsInRange)
                .Returns(mockAddress);
            _mockTarget.ReadInstructionInfos(mockAddress, maxInstructionsInRange + 1, _flavor)
                .Returns(instructions);

            Assert.AreEqual(VSConstants.E_FAIL,
                            _disassemblyStream.Seek(enum_SEEK_START.SEEK_START_CURRENT, null, 0,
                                                    -instructionsToSeek));

            // Location shouldn't change.
            _disassemblyStream.GetCurrentLocation(out ulong newLocation);
            Assert.AreEqual(_testAddress, newLocation);
        }

        [Test]
        public void SeekNegativeInvalidAddress()
        {
            uint instructionsToSeek = 10;
            uint maxInstructionsInRange = 150;
            MockStartInstruction();

            // Add instructions with invalid addresses.
            AddInstructions(maxInstructionsInRange, _testAddress - maxInstructionsInRange, false);

            Assert.AreEqual(VSConstants.E_FAIL,
                            _disassemblyStream.Seek(enum_SEEK_START.SEEK_START_CURRENT, null, 0,
                                                    -instructionsToSeek));

            // Location shouldn't change.
            _disassemblyStream.GetCurrentLocation(out ulong newLocation);
            Assert.AreEqual(_testAddress, newLocation);
        }

        [Test]
        public void SeekNegativeCloseToInstructionStreamEnd()
        {
            uint instructionsToSeek = 10;
            uint instructionSize = 5;
            uint maxInstructionsInRange = 150;
            // We need exactly 30 instructions of size 5 in the seek range.
            uint availableInstructionCount = 30;
            MockStartInstruction();

            // Add instructions starting right at the seek address.
            var startAddress = _testAddress - maxInstructionsInRange;
            var instructions = CreateMockInstructions(availableInstructionCount + 1, startAddress,
                                                      true, instructionSize);
            var mockStartAddress = Substitute.For<SbAddress>();
            _mockTarget.ResolveLoadAddress(startAddress).Returns(mockStartAddress);
            _mockTarget.ReadInstructionInfos(mockStartAddress, maxInstructionsInRange + 1, _flavor)
                .Returns(instructions);

            Assert.AreEqual(VSConstants.S_OK,
                            _disassemblyStream.Seek(enum_SEEK_START.SEEK_START_CURRENT, null, 0,
                                                    -instructionsToSeek));

            _disassemblyStream.GetCurrentLocation(out ulong newLocation);
            Assert.AreEqual(_testAddress - instructionsToSeek * instructionSize, newLocation);
        }

        [Test]
        public void SeekNegativeOutOfBoundsLargeInstructions()
        {
            uint instructionsToSeek = 10;
            uint instructionSize = 5;
            uint maxInstructionsInRange = 150;
            // We need 30 instructions, 29 is not enough. Let us test we fail.
            uint availableInstructionCount = 29;
            MockStartInstruction();

            // Add instructions starting right at the seek address.
            var startAddress = _testAddress - maxInstructionsInRange;
            var instructions = CreateMockInstructions(availableInstructionCount + 1, startAddress,
                                                      true, instructionSize);
            var mockStartAddress = Substitute.For<SbAddress>();
            _mockTarget.ResolveLoadAddress(startAddress).Returns(mockStartAddress);
            _mockTarget.ReadInstructionInfos(mockStartAddress, maxInstructionsInRange + 1, _flavor)
                .Returns(instructions);

            Assert.AreEqual(VSConstants.E_FAIL,
                            _disassemblyStream.Seek(enum_SEEK_START.SEEK_START_CURRENT, null, 0,
                                                    -instructionsToSeek));

            // Location shouldn't change.
            _disassemblyStream.GetCurrentLocation(out ulong newLocation);
            Assert.AreEqual(_testAddress, newLocation);
        }

        void MockStartInstruction(ulong address = _testAddress)
        {
            // Setup start instruction.
            var startInstruction = new InstructionInfo();
            var mockStartAddress = Substitute.For<SbAddress>();
            // mockStartInstruction.GetAddress().Returns(mockStartAddress);
            startInstruction.Address = address;
            mockStartAddress.GetLoadAddress(_mockTarget).Returns(address);
            _mockTarget.ResolveLoadAddress(address).Returns(mockStartAddress);
            _mockTarget.ReadInstructionInfos(mockStartAddress, 1, _flavor)
                .Returns(new List<InstructionInfo> { startInstruction });
        }

        List<InstructionInfo> MockRead(uint instructionsToCreate, uint instructionsToRead)
        {
            var instructions = CreateMockInstructions(instructionsToCreate);
            var mockAddress = Substitute.For<SbAddress>();
            _mockTarget.ResolveLoadAddress(_testAddress).Returns(mockAddress);
            mockAddress.GetLoadAddress(_mockTarget).Returns(_testAddress);
            _mockTarget.ReadInstructionInfos(mockAddress, instructionsToRead, _flavor)
                .Returns(instructions);
            return instructions;
        }

        void AddInstructions(uint count, ulong startAddress = _testAddress, bool hasAddress = true)
        {
            // Add an additional instruction to account for the current instruction.
            var instructions = CreateMockInstructions(count + 1, startAddress, hasAddress);
            var mockStartAddress = Substitute.For<SbAddress>();
            _mockTarget.ResolveLoadAddress(startAddress).Returns(mockStartAddress);
            _mockTarget.ReadInstructionInfos(mockStartAddress, count + 1, _flavor)
                .Returns(instructions);
        }

        List<InstructionInfo> CreateMockInstructions(uint count, ulong startAddress = _testAddress,
                                                     bool hasAddress = true,
                                                     uint instructionSize = 1)
        {
            var instructions = new List<InstructionInfo>();
            for (uint i = 0; i < count; i++)
            {
                ulong address = 0;

                if (hasAddress)
                {
                    address = startAddress + i * instructionSize;
                }

                var instruction =
                    new InstructionInfo
                    {
                        Address = address,
                        Mnemonic = _testMnemonic + i,
                        Operands = _testOperands + i,
                        Comment = _testComment
                    };

                instructions.Add(instruction);
            }
            return instructions;
        }

        LineEntryInfo CreateLineEntry(string fileName, string directory, uint line, uint column)
        {
            return new LineEntryInfo
            {
                FileName = fileName,
                Directory = directory,
                Line = line,
                Column = column
            };
        }

        string GetHexString(ulong address) => $"0x{address:x16}";

        string FormatUrl(string directory, string file) => "file://" + Path.Combine(directory,
                                                                                    file);
    }
}

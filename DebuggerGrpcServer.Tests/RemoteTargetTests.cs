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

using LldbApi;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;

namespace DebuggerGrpcServer.Tests
{
    [TestFixture]
    [Timeout(5000)]
    class RemoteTargetTests
    {
        const string TEST_MNEMONIC = "Test Mnemonic";
        const string TEST_OPERANDS = "Test Operands";
        const string TEST_COMMENT = "Test Comment";
        const ulong TEST_ADDRESS = 0x123456789abc;
        const string TEST_SYMBOL = "Test Symbol";
        const string TEST_DIRECTORY = "C:\\";
        const string TEST_FILENAME = "main.cc";
        const uint TEST_LINE = 123456u;
        const uint TEST_COLUMN = 654321u;
        const string TEST_FUNCTION_NAME = "testFunctionName";
        const int EXPECTED_ID = 1234;

        SbTarget mockTarget;
        RemoteTarget remoteTarget;
        SbAddress mockAddress;
        SbProcess mockProcess;
        SbMemoryRegionInfo mockMemoryRegion;
        SbError mockError;
        SbBreakpoint mockBreakpoint;
        RemoteBreakpoint remoteBreakpoint;
        SbFunction mockFunction;

        [SetUp]
        public void SetUp()
        {
            mockTarget = Substitute.For<SbTarget>();
            remoteTarget = new RemoteTargetFactory(new RemoteBreakpointFactory())
                .Create(mockTarget);
            mockAddress = Substitute.For<SbAddress>();
            mockProcess = Substitute.For<SbProcess>();
            mockMemoryRegion = Substitute.For<SbMemoryRegionInfo>();
            mockError = Substitute.For<SbError>();
            mockBreakpoint = Substitute.For<SbBreakpoint>();
            remoteBreakpoint = new RemoteBreakpointFactory().Create(mockBreakpoint);
            mockFunction = Substitute.For<SbFunction>();

            mockTarget.GetProcess().Returns(mockProcess);
        }

        [Test]
        public void ReadFull()
        {
            uint numberInstructions = 20;
            MockRead(numberInstructions, numberInstructions, mockAddress, mockMemoryRegion);

            var instructions = remoteTarget.ReadInstructionInfos(mockAddress, numberInstructions,
                "intel");
            Assert.AreEqual(numberInstructions, instructions.Count);
            Assert.IsNull(instructions[0].SymbolName);
        }

        [Test]
        public void ReadEmpty()
        {
            uint instructionsToRead = 10;

            for (uint i = 0; i < instructionsToRead; i++)
            {
                var mockInvalidAddress = Substitute.For<SbAddress>();
                if (i == 0)
                {
                    mockAddress = mockInvalidAddress;
                }
                ulong address = TEST_ADDRESS + i;
                MockRead(0, instructionsToRead - i, mockInvalidAddress, mockMemoryRegion, address);
            }

            var instructions = remoteTarget.ReadInstructionInfos(mockAddress, instructionsToRead,
                "intel");
            Assert.AreEqual(instructionsToRead, instructions.Count);
        }

        [Test]
        public void ReadInstructionsWithoutAddress()
        {
            uint numberInstructions = 10;

            // Create mock instructions without address
            var mockInstructions = MockRead(numberInstructions, numberInstructions, mockAddress,
                mockMemoryRegion, TEST_ADDRESS, true, false);
            mockTarget
                .ReadInstructions(mockAddress, numberInstructions, "intel")
                .Returns(mockInstructions);

            var instructions = remoteTarget.ReadInstructionInfos(mockAddress, numberInstructions,
                "intel");
            // Should break and return an empty list
            Assert.AreEqual(0, instructions.Count);
        }

        [Test]
        public void ReadWithSingleSymbol()
        {
            uint numberInstructions = 20;
            int symbolPos = 6;
            var mockInstructions = MockRead(numberInstructions, numberInstructions, mockAddress,
                mockMemoryRegion);

            var mockSbAddress = mockInstructions[symbolPos].GetAddress();
            var mockSymbol = Substitute.For<SbSymbol>();
            mockSymbol.GetName().Returns(TEST_SYMBOL);
            // Make sure it's the same address as the function
            mockSymbol.GetStartAddress().Returns(mockSbAddress);
            mockSbAddress.GetSymbol().Returns(mockSymbol);

            var instructions = remoteTarget.ReadInstructionInfos(mockAddress, numberInstructions,
                "intel");
            Assert.AreEqual(numberInstructions, instructions.Count);
            Assert.AreEqual(instructions[symbolPos].SymbolName, TEST_SYMBOL);
        }

        [Test]
        public void ReadWithSingleSymbolWrongAddress()
        {
            uint numberInstructions = 20;
            int symbolPos = 8;
            var mockInstructions = MockRead(numberInstructions, numberInstructions, mockAddress,
                mockMemoryRegion);


            var mockSbAddress = mockInstructions[symbolPos].GetAddress();
            var mockSymbol = Substitute.For<SbSymbol>();
            mockSymbol.GetName().Returns(TEST_SYMBOL);
            // Make sure it returns an address that is not equal to the instruction
            mockSymbol.GetStartAddress().GetLoadAddress(mockTarget).
                Returns(TEST_ADDRESS + 0xdeadbeef);
            mockSbAddress.GetSymbol().Returns(mockSymbol);

            var instructions = remoteTarget.ReadInstructionInfos(mockAddress, numberInstructions,
                "intel");
            Assert.AreEqual(numberInstructions, instructions.Count);
            Assert.AreEqual(instructions[symbolPos].SymbolName, null);
        }

        [Test]
        public void ReadWithSingleLineEntry()
        {
            uint numberInstructions = 20;
            int lineEntryPos = 9;
            var mockInstructions = MockRead(numberInstructions, numberInstructions, mockAddress,
                mockMemoryRegion);


            var mockSbAddress = mockInstructions[lineEntryPos].GetAddress();
            var mockLineEntry = Substitute.For<SbLineEntry>();
            mockLineEntry.GetFileName().Returns(TEST_FILENAME);
            mockLineEntry.GetDirectory().Returns(TEST_DIRECTORY);
            mockLineEntry.GetLine().Returns(TEST_LINE);
            mockLineEntry.GetColumn().Returns(TEST_COLUMN);
            mockSbAddress.GetLineEntry().Returns(mockLineEntry);

            var instructions = remoteTarget.ReadInstructionInfos(mockAddress, numberInstructions,
                "intel");
            Assert.AreEqual(numberInstructions, instructions.Count);
            Assert.AreEqual(instructions[lineEntryPos].LineEntry.FileName, TEST_FILENAME);
            Assert.AreEqual(instructions[lineEntryPos].LineEntry.Directory, TEST_DIRECTORY);
            Assert.AreEqual(instructions[lineEntryPos].LineEntry.Line, TEST_LINE);
            Assert.AreEqual(instructions[lineEntryPos].LineEntry.Column, TEST_COLUMN);
        }

        [Test]
        public void ReadWithInvalidInstruction()
        {
            uint numberInstructions = 20;
            uint invalidPos = 7;
            uint numberInstructionsAfter = numberInstructions - invalidPos - 1u;

            var mockBeforeAddress = Substitute.For<SbAddress>();
            var mockAfterAddress = Substitute.For<SbAddress>();

            // Make sure that it will resolve to the correct address after the invalid instruction
            mockTarget.ResolveLoadAddress(TEST_ADDRESS + invalidPos + 1).Returns(mockAfterAddress);

            // Create valid instructions up to |invalidPos|
            var mockBeforeInvalidInstructions = MockRead(invalidPos, numberInstructions,
                mockBeforeAddress, mockMemoryRegion);

            var mockAfterInvalidInstructions = MockRead(numberInstructionsAfter,
                numberInstructionsAfter, mockAfterAddress, mockMemoryRegion);

            var instructions = remoteTarget.ReadInstructionInfos(mockBeforeAddress,
                numberInstructions, "intel");

            var invalidInstruction = instructions[(int)invalidPos];
            Assert.AreEqual(numberInstructions, instructions.Count);
            Assert.AreEqual("??", invalidInstruction.Operands);
            Assert.AreEqual("??", invalidInstruction.Mnemonic);
        }

        [Test]
        public void ReadOutsideOfProcessMemory()
        {
            uint numberInstructions = 20;
            MockRead(numberInstructions, numberInstructions, mockAddress, mockMemoryRegion,
                TEST_ADDRESS, false);

            var instructions = remoteTarget.ReadInstructionInfos(mockAddress, numberInstructions,
                "intel");

            // Make sure we did not try to disassemble
            mockTarget.DidNotReceiveWithAnyArgs().ReadInstructions(
                Substitute.For<SbAddress>(), 0, "");
            Assert.AreEqual(numberInstructions, instructions.Count);
        }

        // Tries to read across memory regions where the first is mapped and the second is unmapped
        [Test]
        public void ReadAcrossMemoryRegionsMappedUnmapped()
        {
            uint instructionsToRead = 20;
            uint instructionsToCreate = 10;

            var mockFirstAddress = Substitute.For<SbAddress>();
            var mockSecondAddress = Substitute.For<SbAddress>();

            var mockFirstMemoryRegion = Substitute.For<SbMemoryRegionInfo>();
            var mockSecondMemoryRegion = Substitute.For<SbMemoryRegionInfo>();

            ulong firstAddress = TEST_ADDRESS;
            ulong secondAddress = firstAddress + instructionsToCreate + 1;

            MockRead(instructionsToCreate, instructionsToRead, mockFirstAddress,
                mockFirstMemoryRegion, firstAddress);
            MockRead(0, 0, mockSecondAddress, mockSecondMemoryRegion, secondAddress, false);

            var instructions = remoteTarget.ReadInstructionInfos(mockFirstAddress,
                instructionsToRead, "intel");

            mockTarget.Received(1).ReadInstructions(mockFirstAddress, instructionsToRead, "intel");
            Assert.AreEqual(instructionsToRead, instructions.Count);
            Assert.AreNotEqual("??", instructions[(int)instructionsToCreate - 1].Operands);
            Assert.AreEqual("??", instructions[(int)instructionsToCreate].Operands);
        }

        // Tries to read across memory regions where the first is unmapped and the second is mapped
        [Test]
        public void ReadAcrossMemoryRegionsUnmappedMapped()
        {
            uint instructionsToRead = 20;
            uint instructionsToCreate = 10;

            var mockFirstAddress = Substitute.For<SbAddress>();
            var mockSecondAddress = Substitute.For<SbAddress>();

            var mockFirstMemoryRegion = Substitute.For<SbMemoryRegionInfo>();
            var mockSecondMemoryRegion = Substitute.For<SbMemoryRegionInfo>();

            ulong firstAddress = TEST_ADDRESS;
            ulong secondAddress = firstAddress + instructionsToCreate;

            MockRead(instructionsToCreate, instructionsToRead,
                mockFirstAddress, mockFirstMemoryRegion, firstAddress, false, true,
                firstAddress + instructionsToCreate);
            MockRead(instructionsToRead - instructionsToCreate,
                instructionsToRead - instructionsToCreate,
                mockSecondAddress, mockSecondMemoryRegion, secondAddress);

            var instructions = remoteTarget.ReadInstructionInfos(mockFirstAddress,
                instructionsToRead, "intel");

            mockTarget.Received(1).ReadInstructions(mockSecondAddress,
                instructionsToRead - instructionsToCreate, "intel");
            Assert.AreEqual(instructionsToRead, instructions.Count);
            Assert.AreEqual("??", instructions[(int)instructionsToCreate - 1].Operands);
            Assert.AreNotEqual("??", instructions[(int)instructionsToCreate].Operands);
        }

        [Test]
        public void ReadMemoryRegionFail()
        {
            uint numberInstructions = 20;

            MockRead(numberInstructions, numberInstructions, mockAddress, mockMemoryRegion);

            mockError.Fail().Returns(true);

            var instructions = remoteTarget.ReadInstructionInfos(mockAddress, numberInstructions,
                "intel");

            Assert.AreEqual(0, instructions.Count);
            mockMemoryRegion.DidNotReceive().IsMapped();
            mockTarget.DidNotReceiveWithAnyArgs().ReadInstructions(mockAddress, numberInstructions,
                "intel");
        }

        [Test]
        public void BindFunctionBreakpointWithOffset()
        {
            MockFunctionBreakpoint(1);

            uint startPosition = 75u;
            uint endPosition = 100u;
            uint offset = 10;
            uint newPosition = startPosition + offset + 1;

            string path = Path.Combine(TEST_DIRECTORY, TEST_FILENAME);
            MockFunctionData(startPosition, endPosition, TEST_DIRECTORY, TEST_FILENAME);

            mockTarget.BreakpointCreateByLocation(path, newPosition)
                .Returns(mockBreakpoint);

            var testBreakpoint = remoteTarget.CreateFunctionOffsetBreakpoint(TEST_FUNCTION_NAME,
                offset);

            mockTarget.Received().BreakpointDelete(EXPECTED_ID);
            mockTarget.Received().BreakpointCreateByLocation(path, newPosition);
            Assert.AreEqual(mockBreakpoint.GetId(), remoteBreakpoint.GetId());
        }

        // Test when offset takes you out of the function
        [Test]
        public void BindInvalidFunctionBreakpointWithOffset()
        {
            MockFunctionBreakpoint(1);

            uint startPosition = 75u;
            uint endPosition = 100u;
            uint offset = endPosition - startPosition + 1;
            uint newPosition = startPosition + offset + 1;

            string path = Path.Combine(TEST_DIRECTORY, TEST_FILENAME);
            MockFunctionData(startPosition, endPosition, TEST_DIRECTORY, TEST_FILENAME);

            mockTarget.BreakpointCreateByLocation(path, newPosition)
                .Returns(mockBreakpoint);

            var testBreakpoint = remoteTarget.CreateFunctionOffsetBreakpoint(TEST_FUNCTION_NAME,
                offset);

            mockTarget.Received().BreakpointDelete(EXPECTED_ID);
            mockTarget.DidNotReceive().BreakpointCreateByLocation(path, newPosition);
            Assert.AreEqual(null, testBreakpoint.breakpoint);
        }

        // Test when function breakpoint is not bound to any location
        [Test]
        public void BindFunctionBreakpointWithOffsetZeroLocations()
        {
            MockFunctionBreakpoint(0);

            uint startPosition = 75u;
            uint endPosition = 100u;
            uint offset = endPosition - startPosition + 1;
            uint newPosition = startPosition + offset + 1;

            string path = Path.Combine(TEST_DIRECTORY, TEST_FILENAME);
            MockFunctionData(startPosition, endPosition, TEST_DIRECTORY, TEST_FILENAME);

            mockTarget.BreakpointCreateByLocation(path, newPosition)
                .Returns(mockBreakpoint);

            var testBreakpoint = remoteTarget.CreateFunctionOffsetBreakpoint(TEST_FUNCTION_NAME,
                offset);

            mockTarget.DidNotReceive().BreakpointDelete(Arg.Any<int>());
            mockTarget.DidNotReceive().BreakpointCreateByLocation(path, newPosition);
            Assert.AreEqual(null, testBreakpoint.breakpoint);
        }

        // Test when function cannot be found
        [Test]
        public void BindFunctionBreakpointWithOffsetNoFunction()
        {
            MockFunctionBreakpoint(1);

            uint startPosition = 75u;
            uint endPosition = 100u;
            uint offset = endPosition - startPosition + 1;
            uint newPosition = startPosition + offset + 1;

            string path = Path.Combine(TEST_DIRECTORY, TEST_FILENAME);
            MockFunctionData(startPosition, endPosition, TEST_DIRECTORY, TEST_FILENAME);

            mockTarget.BreakpointCreateByLocation(path, newPosition)
                .Returns(mockBreakpoint);

            var testBreakpoint = remoteTarget.CreateFunctionOffsetBreakpoint(TEST_FUNCTION_NAME,
                offset);

            mockTarget.Received().BreakpointDelete(EXPECTED_ID);
            mockTarget.DidNotReceive().BreakpointCreateByLocation(path, newPosition);
            Assert.AreEqual(null, testBreakpoint.breakpoint);
        }

        private void MockFunctionData(uint startPosition, uint endPosition,
            string directory, string fileName)
        {
            SbBreakpointLocation location = mockBreakpoint.GetLocationAtIndex(0);

            SbAddress mockBreakpointAddress = Substitute.For<SbAddress>();
            SbAddress mockStartAddress = Substitute.For<SbAddress>();
            SbAddress mockFunctionEndAddress = Substitute.For<SbAddress>();
            SbAddress mockActualEndAddress = Substitute.For<SbAddress>();

            SbLineEntry mockStartLineEntry = Substitute.For<SbLineEntry>();
            SbLineEntry mockEndLineEntry = Substitute.For<SbLineEntry>();

            ulong address = 0x1234567;

            location.GetAddress().Returns(mockBreakpointAddress);
            mockBreakpointAddress.GetFunction().Returns(mockFunction);

            mockFunction.GetStartAddress().Returns(mockStartAddress);
            mockFunction.GetEndAddress().Returns(mockFunctionEndAddress);

            mockFunctionEndAddress.GetLoadAddress(mockTarget).Returns(address);
            mockTarget.ResolveLoadAddress(address - 1).Returns(mockActualEndAddress);

            mockStartAddress.GetLineEntry().Returns(mockStartLineEntry);
            mockActualEndAddress.GetLineEntry().Returns(mockEndLineEntry);

            mockStartLineEntry.GetLine().Returns(startPosition);
            mockStartLineEntry.GetDirectory().Returns(directory);
            mockStartLineEntry.GetFileName().Returns(fileName);
            mockEndLineEntry.GetLine().Returns(endPosition);
        }

        // Create default mocks, and return values for the lldb breakpoint and breakpoint locations
        // for a function breakpoint.  numBreakpointLocations specifies how many mock breakpoint
        // locations to return.
        private void MockFunctionBreakpoint(int numBreakpointLocations)
        {
            List<SbBreakpointLocation> breakpointLocations =
                CreateMockBreakpointLocations(numBreakpointLocations);
            MockFunctionBreakpoint(breakpointLocations);
        }

        // Create default mocks, and return values for the lldb breakpoint and breakpoint locations
        // for a function breakpoint.  breakpointLocations is a list of mock breakpoint locations
        // that will be returned by the mock lldb breakpoint.
        private void MockFunctionBreakpoint(List<SbBreakpointLocation> breakpointLocations)
        {
            for (uint i = 0; i < breakpointLocations.Count; i++)
            {
                mockBreakpoint.GetLocationAtIndex(i).Returns(breakpointLocations[(int)i]);
            }
            mockBreakpoint.GetNumLocations().Returns((uint)breakpointLocations.Count);
            mockBreakpoint.GetId().Returns(EXPECTED_ID);
            mockTarget.BreakpointCreateByName(TEST_FUNCTION_NAME).Returns(mockBreakpoint);
        }

        private List<SbBreakpointLocation> CreateMockBreakpointLocations(
            int numBreakpointLocations)
        {
            List<SbBreakpointLocation> breakpointLocations =
                new List<SbBreakpointLocation>(numBreakpointLocations);
            for (int i = 0; i < numBreakpointLocations; i++)
            {
                var mockBreakpointLocation = Substitute.For<SbBreakpointLocation>();
                mockBreakpointLocation.GetId().Returns(i);
                breakpointLocations.Add(mockBreakpointLocation);
            }
            return breakpointLocations;
        }

        private List<SbInstruction> MockRead(uint instructionsToCreate, uint instructionsToRead,
            SbAddress startSbAddress, SbMemoryRegionInfo memoryRegion,
            ulong startAddress = TEST_ADDRESS, bool isMapped = true,
            bool hasAddress = true, ulong regionEnd = ulong.MaxValue)
        {
            var instructions = CreateMockInstructions(instructionsToCreate, startAddress,
                hasAddress);
            mockTarget
                .ReadInstructions(startSbAddress, instructionsToRead, "intel")
                .Returns(instructions);

            startSbAddress.GetLoadAddress(mockTarget).Returns(startAddress);
            mockTarget.ResolveLoadAddress(startAddress).Returns(startSbAddress);

            memoryRegion.IsMapped().Returns(isMapped);
            if (!isMapped)
            {
                memoryRegion.GetRegionEnd().Returns(regionEnd);
            }

            SbMemoryRegionInfo memRegion;
            mockProcess.GetMemoryRegionInfo(startAddress, out memRegion).Returns(
                x =>
                {
                    x[1] = memoryRegion;
                    return mockError;
                });

            return instructions;
        }

        private List<SbInstruction> CreateMockInstructions(uint count,
            ulong startAddress = TEST_ADDRESS, bool hasAddress = true)
        {
            List<SbInstruction> instructions = new List<SbInstruction>();
            for (uint i = 0; i < count; i++)
            {
                SbAddress mockSbAddress = null;
                if (hasAddress)
                {
                    mockSbAddress = Substitute.For<SbAddress>();
                    mockSbAddress
                        .GetLoadAddress(mockTarget)
                        .Returns(startAddress + i);
                    mockSbAddress.GetSymbol().Returns((SbSymbol)null);
                    mockSbAddress.GetLineEntry().Returns((SbLineEntry)null);
                }

                var mockInstruction = Substitute.For<SbInstruction>();
                mockInstruction.GetAddress().Returns(mockSbAddress);
                mockInstruction.GetMnemonic(mockTarget).Returns(TEST_MNEMONIC + i);
                mockInstruction.GetOperands(mockTarget).Returns(TEST_OPERANDS + i);
                mockInstruction.GetComment(mockTarget).Returns(TEST_COMMENT);
                mockInstruction.GetByteSize().Returns(1u);

                instructions.Add(mockInstruction);
            }
            return instructions;
        }
    }
}

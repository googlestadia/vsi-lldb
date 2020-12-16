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

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using NSubstitute;
using NUnit.Framework;
using YetiVSI.PortSupplier;

namespace YetiVSI.Test.PortSupplier
{
    [TestFixture]
    class DebugProcessTests
    {
        private const int TEST_PID = 123;
        private const string TEST_TITLE = "test title";
        private const string TEST_COMMAND = "test command";

        IDebugProcess2 process;
        IDebugProgram2 program;

        [SetUp]
        public void SetUp()
        {
            program = Substitute.For<IDebugProgram2>();
            var debugProgramFactory = Substitute.For<DebugProgram.Factory>();
            debugProgramFactory.Create(Arg.Any<DebugProcess>()).Returns(program);

            var port = Substitute.For<IDebugPort2>();

            process = new DebugProcess.Factory(debugProgramFactory).Create(
                port, TEST_PID, TEST_TITLE, TEST_COMMAND);
        }

        [Test]
        public void EnumPrograms()
        {
            IEnumDebugPrograms2 programEnum;
            Assert.AreEqual(VSConstants.S_OK, process.EnumPrograms(out programEnum));

            uint count;
            Assert.AreEqual(VSConstants.S_OK, programEnum.GetCount(out count));
            Assert.AreEqual(1, count);

            uint num = 1;
            var programs = new IDebugProgram2[1];
            uint numFetched = 0;
            Assert.AreEqual(VSConstants.S_OK, programEnum.Next(num, programs, ref numFetched));
            Assert.AreEqual(1, numFetched);
            Assert.AreEqual(programs[0], program);
        }
    }
}

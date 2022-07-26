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
        IDebugProcess2 _process;

        [SetUp]
        public void SetUp()
        {
            var port = Substitute.For<IDebugPort2>();

            _process = new DebugProcess(
                port: port, pid: 1337, ppid: 42, title: "Title", command: "/bin/true");
        }

        [Test]
        public void EnumPrograms()
        {
            Assert.AreEqual(
                VSConstants.S_OK, _process.EnumPrograms(out IEnumDebugPrograms2 programEnum));
            Assert.AreEqual(VSConstants.S_OK, programEnum.GetCount(out uint count));
            Assert.AreEqual(1, count);

            uint num = 1;
            var programs = new IDebugProgram2[1];
            uint numFetched = 0;
            Assert.AreEqual(VSConstants.S_OK, programEnum.Next(num, programs, ref numFetched));
            Assert.AreEqual(1, numFetched);
            programs[0].GetName(out string name);
            Assert.AreEqual(name, "/bin/true");
        }
    }
}

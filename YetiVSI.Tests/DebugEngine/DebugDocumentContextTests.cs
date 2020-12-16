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

using DebuggerCommonApi;
using Microsoft.VisualStudio.Debugger.Interop;
using NSubstitute;
using NUnit.Framework;
using YetiVSI.DebugEngine;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class DebugDocumentContextTests
    {
        LineEntryInfo lineEntry;

        DebugDocumentContext.Factory documentContextFactory;
        IDebugDocumentContext2 documentContext;

        [SetUp]
        public void SetUp()
        {
            lineEntry = new LineEntryInfo();
            documentContextFactory = new DebugDocumentContext.Factory();
            documentContext = documentContextFactory.Create(lineEntry);
        }

        [Test]
        public void GetName()
        {
            lineEntry.Directory = "dir";
            lineEntry.FileName = "file";
            string fileName;
            documentContext.GetName(enum_GETNAME_TYPE.GN_FILENAME, out fileName);
            Assert.AreEqual("dir\\file", fileName);
        }

        [Test]
        public void GetStatementRangeZero()
        {
            uint lldbLineNumber = 0;
            uint lldbColumnNumber = 0;
            lineEntry.Line = lldbLineNumber;
            lineEntry.Column = lldbColumnNumber;
            TEXT_POSITION[] start = new TEXT_POSITION[1];
            TEXT_POSITION[] end = new TEXT_POSITION[1];
            documentContext.GetStatementRange(start, end);

            Assert.AreEqual(lldbLineNumber, start[0].dwLine);
            Assert.AreEqual(lldbColumnNumber, start[0].dwColumn);
            Assert.AreEqual(lldbLineNumber, end[0].dwLine);
            Assert.AreEqual(0, end[0].dwColumn);
        }

        [Test]
        public void GetStatementRangeOne()
        {
            uint lldbLineNumber = 1;
            uint lldbColumnNumber = 1;
            lineEntry.Line = lldbLineNumber;
            lineEntry.Column = lldbColumnNumber;
            TEXT_POSITION[] start = new TEXT_POSITION[1];
            TEXT_POSITION[] end = new TEXT_POSITION[1];
            documentContext.GetStatementRange(start, end);

            Assert.AreEqual(lldbLineNumber - 1, start[0].dwLine);
            Assert.AreEqual(lldbColumnNumber - 1, start[0].dwColumn);
            Assert.AreEqual(lldbLineNumber, end[0].dwLine);
            Assert.AreEqual(0, end[0].dwColumn);
        }

        [Test]
        public void GetStatementRange()
        {
            uint lldbLineNumber = 54;
            uint lldbColumnNumber = 12;
            lineEntry.Line = lldbLineNumber;
            lineEntry.Column = lldbColumnNumber;
            TEXT_POSITION[] start = new TEXT_POSITION[1];
            TEXT_POSITION[] end = new TEXT_POSITION[1];
            documentContext.GetStatementRange(start, end);

            Assert.AreEqual(lldbLineNumber - 1, start[0].dwLine);
            Assert.AreEqual(lldbColumnNumber - 1, start[0].dwColumn);
            Assert.AreEqual(lldbLineNumber, end[0].dwLine);
            Assert.AreEqual(0, end[0].dwColumn);
        }
    }
}

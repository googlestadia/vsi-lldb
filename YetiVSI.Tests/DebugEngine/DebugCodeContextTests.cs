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
using System;
using YetiVSI.DebugEngine;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class DebugCodeContextTests
    {
        const ulong _testPc = 0x123456789abcdef0;
        const string _testName = "frame name";

        IDebugDocumentContext2 _mockDocumentContext;
        DebugMemoryContext.Factory _mockMemoryContextFactory;

        IDebugCodeContext2 _codeContext;

        [SetUp]
        public void SetUp()
        {
            _mockDocumentContext = Substitute.For<IDebugDocumentContext2>();
            _mockMemoryContextFactory = Substitute.For<DebugMemoryContext.Factory>();
            _codeContext = new DebugCodeContext.Factory(_mockMemoryContextFactory)
                               .Create(_testPc, _testName, _mockDocumentContext, Guid.Empty);
        }

        [Test]
        public void GetAddress()
        {
            Assert.AreEqual(_testPc, _codeContext.GetAddress());
        }

        [Test]
        public void GetDocumentContext()
        {
            Assert.AreEqual(VSConstants.S_OK, _codeContext.GetDocumentContext(
                                                  out IDebugDocumentContext2 documentContext));
            Assert.AreEqual(_mockDocumentContext, documentContext);
        }

        [Test]
        public void GetDocumentContext_Null()
        {
            _codeContext = new DebugCodeContext.Factory(_mockMemoryContextFactory)
                               .Create(_testPc, _testName, null, Guid.Empty);

            Assert.AreEqual(VSConstants.S_FALSE, _codeContext.GetDocumentContext(
                                                     out IDebugDocumentContext2 documentContext));
            Assert.AreEqual(null, documentContext);
        }

        [Test]
        public void GetLanguageInfo()
        {
            string language = null;
            var guid = new Guid();
            Assert.AreEqual(VSConstants.S_OK, _codeContext.GetLanguageInfo(ref language, ref guid));
            Assert.That(guid, Is.EqualTo(Guid.Empty));
        }

        [TestCase(AD7Constants.CLanguage, "C")]
        [TestCase(AD7Constants.CppLanguage, "C++")]
        [TestCase(AD7Constants.CSharpLanguage, "C#")]
        [TestCase("63A08714-FC37-11D2-904C-00C04FA302A2", "")]
        [TestCase("00000000-0000-0000-0000-000000000000", "")]
        public void GetLanguageInfoWhenProvided(string guidAsString, string correspondingLanguage)
        {
            var guid = new Guid(guidAsString);
            IDebugCodeContext2 context =
                new DebugCodeContext.Factory(_mockMemoryContextFactory)
                    .Create(_testPc, _testName, _mockDocumentContext, guid);
            var outputGuid = Guid.Empty;
            var outputLanguage = string.Empty;
            int result = context.GetLanguageInfo(ref outputLanguage, ref outputGuid);
            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
            Assert.That(outputGuid, Is.EqualTo(outputGuid));
            Assert.That(outputLanguage, Is.EqualTo(correspondingLanguage));
        }
    }
}

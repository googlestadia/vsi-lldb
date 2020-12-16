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

using NUnit.Framework;
using NSubstitute;
using YetiVSI.DebugEngine;
using YetiCommon;
using YetiVSI.DebugEngine.Interfaces;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class LldbTransportSessionTests
    {
        const string FILE_PREFIX = "YetiTransportSession";
        MemoryMappedFileFactory mockMemoryMappedFileFactory;
        IMemoryMappedFile mockMemoryMappedFile;
        ITransportSession transportSession;
        LldbTransportSession.Factory transportSessionFactory;

        [SetUp]
        public void Setup()
        {
            mockMemoryMappedFileFactory = Substitute.For<MemoryMappedFileFactory>();
            mockMemoryMappedFile = Substitute.For<IMemoryMappedFile>();

            // The first session should get a memory mapped file on the first try.
            mockMemoryMappedFileFactory.CreateNew(FILE_PREFIX + 0, Arg.Any<long>()).Returns(
                mockMemoryMappedFile);
            transportSessionFactory = new LldbTransportSession.Factory(mockMemoryMappedFileFactory);
            transportSession = transportSessionFactory.Create();
        }

        [Test]
        public void MultipleSessions()
        {
            // The second session should throw an exception when creating the first file (because it
            // already exists), and get a memory mapped file on the second try.
            mockMemoryMappedFileFactory.CreateNew(FILE_PREFIX + 0, Arg.Any<long>()).Returns(x =>
                {
                    throw new System.IO.IOException();
                });
            mockMemoryMappedFileFactory.CreateNew(FILE_PREFIX + 1, Arg.Any<long>()).Returns(
                mockMemoryMappedFile);
            var transportSession2 = transportSessionFactory.Create();

            Assert.AreNotEqual(LldbTransportSession.INVALID_SESSION_ID,
                transportSession.GetSessionId());
            Assert.AreNotEqual(LldbTransportSession.INVALID_SESSION_ID,
                transportSession2.GetSessionId());
            Assert.AreNotEqual(transportSession.GetSessionId(), transportSession2.GetSessionId());

            // Make sure the port numbers are different for the two sessions.
            Assert.AreNotEqual(transportSession.GetLocalDebuggerPort(),
                transportSession2.GetLocalDebuggerPort());
            Assert.AreNotEqual(transportSession.GetReservedLocalAndRemotePort(),
                transportSession2.GetReservedLocalAndRemotePort());
        }

        [Test]
        public void DisposeSuccess()
        {
            transportSession.Dispose();
            mockMemoryMappedFile.Received().Dispose();
        }

        [Test]
        public void InvalidSession()
        {
            // Throw an exception for every create call so we get an invalid session.
            mockMemoryMappedFileFactory.CreateNew(Arg.Any<string>(), Arg.Any<long>()).Returns(x =>
                {
                    throw new System.IO.IOException();
                });
            var invalidTransportSession = transportSessionFactory.Create();
            Assert.IsNull(invalidTransportSession);
        }

        [Test]
        public void InvalidMappedFile()
        {
            // Throw an exception for every create call so we get an invalid session.
            mockMemoryMappedFileFactory.CreateNew(Arg.Any<string>(), Arg.Any<long>()).Returns(
                (IMemoryMappedFile)null);
            var invalidTransportSession = transportSessionFactory.Create();
            Assert.IsNull(invalidTransportSession);
        }
    }
}
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
using System;

namespace YetiVSI.Test.TestSupport
{
    [TestFixture]
    class KeyStubTests
    {
        KeyStub keyStub;

        [SetUp]
        public void SetUp()
        {
            const string keyName = "parentKey";
            keyStub = new KeyStub(keyName);
        }

        [Test]
        public void CreateSubkeySameName()
        {
            const string keyName = "key1";
            keyStub.CreateSubkey(keyName);
            Assert.Throws<ArgumentException>(() => keyStub.CreateSubkey(keyName),
                "Expected test double to throw when adding two keys with the same name.");
        }
    }
}

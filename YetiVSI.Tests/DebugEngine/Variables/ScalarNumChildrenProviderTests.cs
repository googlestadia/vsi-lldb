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

﻿using NUnit.Framework;
using YetiVSI.DebugEngine.Variables;
using YetiVSI.Test.TestSupport;

namespace YetiVSI.Test.DebugEngine.Variables
{
    [TestFixture]
    class ScalarNumChildrenProviderTests
    {
        [Test]
        public void GetSpecifierReturnsSize()
        {
            var provider = new ScalarNumChildrenProvider(10);
            Assert.That(provider.Specifier, Is.EqualTo("10"));
        }

        [Test]
        public void GetNumChildrenReturnsSize()
        {
            var provider = new ScalarNumChildrenProvider(22);
            var remoteValue = new RemoteValueFake("myVar", "");
            Assert.That(provider.GetNumChildren(remoteValue), Is.EqualTo(22));
        }
    }
}

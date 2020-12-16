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

﻿using NSubstitute;
using NUnit.Framework;
using YetiCommon;

namespace YetiCommon.Tests
{
    [TestFixture]
    class CredentialManagerTests
    {
        private const string TEST_ACCOUNT1 = "test account1";
        private const string TEST_ACCOUNT2 = "test account2";

        [Test]
        public void LoadFromDefaultNoUser()
        {
            var credentialConfigFactory = Substitute.For<CredentialConfig.Factory>();
            credentialConfigFactory.LoadOrDefault()
                .Returns(new CredentialConfig { DefaultAccount = TEST_ACCOUNT1 });
            var credentialManager = new CredentialManager(credentialConfigFactory);
            Assert.AreEqual(TEST_ACCOUNT1, credentialManager.LoadAccount());
        }

        [Test]
        public void LoadFromDefaultEmptyUser()
        {
            var credentialConfigFactory = Substitute.For<CredentialConfig.Factory>();
            credentialConfigFactory.LoadOrDefault()
                .Returns(new CredentialConfig { DefaultAccount = TEST_ACCOUNT1 });
            var accountOptionLoader = Substitute.For<IAccountOptionLoader>();
            accountOptionLoader.LoadAccountOption().Returns("");
            var credentialManager =
                new CredentialManager(credentialConfigFactory, accountOptionLoader);
            Assert.AreEqual(TEST_ACCOUNT1, credentialManager.LoadAccount());
        }

        [Test]
        public void LoadFromUser()
        {
            var credentialConfigFactory = Substitute.For<CredentialConfig.Factory>();
            credentialConfigFactory.LoadOrDefault()
                .Returns(new CredentialConfig { DefaultAccount = TEST_ACCOUNT1 });
            var accountOptionLoader = Substitute.For<IAccountOptionLoader>();
            accountOptionLoader.LoadAccountOption().Returns(TEST_ACCOUNT2);
            var credentialManager =
                new CredentialManager(credentialConfigFactory, accountOptionLoader);
            Assert.AreEqual(TEST_ACCOUNT2, credentialManager.LoadAccount());
        }

        [Test]
        public void LoadNoDefault()
        {
            var credentialConfigFactory = Substitute.For<CredentialConfig.Factory>();
            credentialConfigFactory.LoadOrDefault().Returns(new CredentialConfig{});
            var accountOptionLoader = Substitute.For<IAccountOptionLoader>();
            accountOptionLoader.LoadAccountOption().Returns("");
            var credentialManager =
                new CredentialManager(credentialConfigFactory, accountOptionLoader);
            Assert.AreEqual("", credentialManager.LoadAccount());
        }
    }
}

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

using GgpGrpc;
using GgpGrpc.Cloud;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Util;
using NSubstitute;
using NUnit.Framework;
using System;
using System.IO;
using System.IO.Abstractions.TestingHelpers;

namespace YetiCommon.Tests.Cloud
{
    [TestFixture]
    class CloudConnectionTests
    {
        const string credentialPath = @"\cred\foo";
        const string credentialData = @"{
            ""email"": ""foo@example.com"",
            ""access_token"": ""access"",
            ""refresh_token"": ""refresh"",
            ""expires_at"": ""1557950975"",
            ""scopes"": [
                ""https://www.googleapis.com/foo"",
            ]
        }";

        const string credentialPathBar = @"\cred\bar";
        const string credentialDataBar = @"{
            ""email"": ""bar@example.com"",
            ""access_token"": ""access2"",
            ""refresh_token"": ""refresh2"",
            ""expires_at"": ""1557950975"",
            ""scopes"": [
                ""https://www.googleapis.com/foo"",
            ]
        }";

        const string credentialPathScopes = @"\cred\scopes";
        const string credentialDataScopes = @"{
            ""email"": ""bar@example.com"",
            ""access_token"": ""access2"",
            ""refresh_token"": ""refresh2"",
            ""expires_at"": ""1557950975"",
            ""scopes"": [
                ""https://www.googleapis.com/bar"",
            ]
        }";

        const string invalidCredentialFilePath = @"\cred\invalid";
        const string invalidCredentialDirPath = @"\invalid\foo";

        const string corruptCredentialPath = @"\cred\corrupt";
        const string corruptCredentialData = @"garbage";

        const string emptyCredentialPath = @"\cred\empty";
        const string emptyCredentialData = @"";

        const string corruptExpiryCredentialPath = @"\cred\corrupt_expiry";
        const string correptExpiryCredentialData = @"{
            ""email"": ""foo@example.com"",
            ""access_token"": ""access"",
            ""refresh_token"": ""refresh"",
            ""expires_at"": ""bad"",
            ""scopes"": [
                ""https://www.googleapis.com/foo/bar"",
            ]
        }";

        const string TestUrl = "https://test-cloudcast-pa.sandbox.googleapis.com/";
        const string TestTarget = "test-cloudcast-pa.sandbox.googleapis.com:443";

        const string DefaultUrl = "https://cloudcast-pa.googleapis.com:443";
        const string DefaultTarget = "cloudcast-pa.googleapis.com:443";

        readonly DateTime credentialExpiry =
                new DateTime(2019, 05, 15, 20, 09, 35, DateTimeKind.Utc);
        readonly TimeSpan oneHour = new TimeSpan(1, 0, 0);

        MockFileSystem filesystem;
        CloudConnection.ChannelFactory channelFactory;
        IClock clock;

        // Object under test
        CloudConnection connection;

        [SetUp]
        public void SetUp()
        {
            filesystem = new MockFileSystem();
            filesystem.AddDirectory(Path.GetDirectoryName(credentialPath));
            filesystem.AddFile(credentialPath, new MockFileData(credentialData));
            filesystem.AddFile(credentialPathBar, new MockFileData(credentialDataBar));
            filesystem.AddFile(credentialPathScopes, new MockFileData(credentialDataScopes));
            filesystem.AddFile(corruptCredentialPath, new MockFileData(corruptCredentialData));
            filesystem.AddFile(emptyCredentialPath, new MockFileData(emptyCredentialData));
            filesystem.AddFile(corruptExpiryCredentialPath, 
                new MockFileData(correptExpiryCredentialData));

            // Use a partial mock (actually a test spy) for channel factory, because there
            // is no good way to fake a channel. However, it's safe to create an unused channel!
            channelFactory = Substitute.ForPartsOf<CloudConnection.ChannelFactory>();

            clock = Substitute.For<IClock>();

            connection = new CloudConnection(filesystem, channelFactory);
        }

        [TestCase(TestUrl, TestTarget)]
        [TestCase(DefaultUrl, DefaultTarget)]
        public void CreateNewChannel(string url, string expectedTarget)
        {
            clock.UtcNow.Returns(credentialExpiry.Subtract(oneHour));

            var token = default(TokenResponse);
            channelFactory.Create(
                Arg.Any<Uri>(),
                Arg.Do<UserCredential>(u => token = u.Token));

            var channel = connection.CreateChannel(url, credentialPath);

            Assert.That(channel, Is.Not.Null);
            Assert.That(channel.Target, Is.EqualTo(expectedTarget));

            Assert.That(token.AccessToken, Is.EqualTo("access"));
            Assert.That(token.RefreshToken, Is.EqualTo("refresh"));
            Assert.That(token.TokenType, Is.EqualTo("Bearer"));
            Assert.That(token.IsExpired(clock), Is.False);
        }

        [TestCase("")]
        [TestCase(" ")]
        [TestCase("not-a-url")]
        public void CreateNewChannelFailsWithBadUrl(string url)
        {
            Assert.Throws<ChannelConfigurationException>(
                () => connection.CreateChannel(url, credentialPath));
        }

        [Test]
        public void CreateNewChannelWithExpiredToken()
        {
            clock.UtcNow.Returns(credentialExpiry.Add(oneHour));

            var token = default(TokenResponse);
            channelFactory.Create(
                Arg.Any<Uri>(),
                Arg.Do<UserCredential>(u => token = u.Token));

            var channel = connection.CreateChannel(TestUrl, credentialPath);

            Assert.That(channel, Is.Not.Null);
            Assert.That(token.IsExpired(clock), Is.True);
        }

        [Test]
        public void ReuseChannel()
        {
            var channel = connection.CreateChannel(TestUrl, credentialPath);
            var channel2 = connection.CreateChannel(TestUrl, credentialPath);

            Assert.That(channel2, Is.SameAs(channel));
            channelFactory.ReceivedWithAnyArgs(1).Create(null, null);
        }

        [TestCase(DefaultUrl, credentialPath)]
        [TestCase(TestUrl, credentialPathBar)]
        [TestCase(TestUrl, credentialPathScopes)]
        public void ResetChannel(string url2, string path2)
        {
            var channel = connection.CreateChannel(TestUrl, credentialPath);
            var channel2 = connection.CreateChannel(url2, path2);
            Assert.That(channel2, Is.Not.SameAs(channel));
            channelFactory.ReceivedWithAnyArgs(2).Create(null, null);
        }

        [TestCase(invalidCredentialFilePath)]
        [TestCase(invalidCredentialDirPath)]
        [TestCase(corruptCredentialPath)]
        [TestCase(emptyCredentialPath)]
        [TestCase(corruptExpiryCredentialPath)]
        public void CreateChannelFailsWithBadCredentials(string path)
        {
            var ex = Assert.Throws<ChannelConfigurationException>(
                () => connection.CreateChannel(TestUrl, path));
            Assert.That(ex, Is.AssignableTo<IGgpConfigurationError>());
        }

        [TestCase(TestUrl, null)]
        [TestCase(null, credentialPath)]
        public void CreateChannelFailsWithNullArguments(string url, string path)
        {
            Assert.Throws<ArgumentNullException>(
                () => connection.CreateChannel(url, path));
        }

        [TestCase("")]
        [TestCase(" ")]
        [TestCase("http://test.com")]
        public void CreateChannelFailsWithMalformedPath(string path)
        {
            Assert.Throws<ArgumentException>(
                () => connection.CreateChannel(TestUrl, path));
        }
    }
}

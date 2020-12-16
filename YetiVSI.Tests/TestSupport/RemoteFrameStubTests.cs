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

﻿using System.Threading.Tasks;
using NUnit.Framework;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.Test.TestSupport
{
    [TestFixture]
    class RemoteFrameStubTests
    {
        RemoteFrameStub remoteFrameStub;

        [SetUp]
        public void SetUp()
        {
            remoteFrameStub = new RemoteFrameStub();
        }

        [Test]
        public void NotConfiguredForExpression()
        {
            var exception = Assert.ThrowsAsync<RemoteFrameStub.ConfigurationException>(
                async () => await remoteFrameStub.EvaluateExpressionAsync("any_expression"));
            Assert.That(exception.Message, Does.Contain("any_expression"));
        }

        [Test]
        public async Task ConfiguredForExpressionsInOrderAsync()
        {
            remoteFrameStub = new RemoteFrameStub.Builder()
                .AddExpressionResult("expression1",
                    RemoteValueFakeUtil.CreateSimpleInt("e1", 22))
                .AddExpressionResult("expression2",
                    RemoteValueFakeUtil.CreateSimpleInt("e2", 44))
                .Build();
            var result = await remoteFrameStub.EvaluateExpressionAsync("expression1");
            Assert.That(result.GetName(), Is.EqualTo("e1"));
            Assert.That(result.GetDefaultValue(), Is.EqualTo("22"));
            result = await remoteFrameStub.EvaluateExpressionAsync("expression2");
            Assert.That(result.GetName(), Is.EqualTo("e2"));
            Assert.That(result.GetDefaultValue(), Is.EqualTo("44"));
        }

        [Test]
        public void ConfiguredForExpressionsOutOfOrder()
        {
            remoteFrameStub = new RemoteFrameStub.Builder()
                .AddExpressionResult("expression1",
                    RemoteValueFakeUtil.CreateSimpleInt("e1", 44))
                .AddExpressionResult("expression2",
                    RemoteValueFakeUtil.CreateSimpleInt("e2", 22))
                .Build();
            var exception = Assert.ThrowsAsync<RemoteFrameStub.ConfigurationException>(
                async () => await remoteFrameStub.EvaluateExpressionAsync("expression2"));

            Assert.That(exception.Message, Does.Contain("expression1"));
            Assert.That(exception.Message, Does.Contain("expression2"));
        }
    }
}

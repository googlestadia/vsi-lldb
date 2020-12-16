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
using TestsCommon.TestSupport;
using YetiVSI.DebugEngine.Variables;
using YetiVSI.Test.TestSupport;

namespace YetiVSI.Test.DebugEngine.Variables
{
    [TestFixture]
    class ExpressionNumChildrenProviderTests
    {
        LogSpy logSpy;

        [SetUp]
        public void SetUp()
        {
            logSpy = new LogSpy();
            logSpy.Attach();
        }

        [TearDown]
        public void TearDown()
        {
            logSpy.Detach();
        }

        [Test]
        public void TestGetNumChildren_WhenExpressionCantBeEvaluated()
        {
            var remoteValue = new RemoteValueFake("myValue", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("[0]", 3));
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("[1]", 4));

            var provider = new ExpressionNumChildrenProvider("someExpression", null);
            var numChildren = provider.GetNumChildren(remoteValue);

            Assert.That(numChildren, Is.EqualTo(2));
            Assert.That(logSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(logSpy.GetOutput(), Does.Contain("someExpression"));
        }

        [Test]
        public void TestGetNumChildren_WhenExpressionEvaluatesToNotAUint()
        {
            var remoteValue = new RemoteValueFake("myValue", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("[0]", 3));
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("[1]", 4));
            remoteValue.AddValueFromExpression("someExpression",
                RemoteValueFakeUtil.CreateSimpleChar("someVal", 'c'));

            var provider = new ExpressionNumChildrenProvider("someExpression", null);
            var numChildren = provider.GetNumChildren(remoteValue);

            Assert.That(numChildren, Is.EqualTo(2));
            Assert.That(logSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(logSpy.GetOutput(), Does.Contain("someExpression"));
        }

        [Test]
        public void TestGetNumChildren_WhenLessThanArraySize()
        {
            var remoteValue = new RemoteValueFake("myValue", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("[0]", 3));
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("[1]", 4));
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("[2]", 5));
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("[3]", 6));
            remoteValue.AddValueFromExpression("someExpression",
                RemoteValueFakeUtil.CreateSimpleInt("someVal", 1));

            var provider = new ExpressionNumChildrenProvider("someExpression", null);
            var numChildren = provider.GetNumChildren(remoteValue);

            Assert.That(numChildren, Is.EqualTo(1));
        }

        [Test]
        public void TestGetNumChildren_WhenLessThanArraySizeWithSizeContext()
        {
            var contextValue = RemoteValueFakeUtil.CreateClass("C", "c", "");
            var remoteValue = new RemoteValueFake("myValue", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("[0]", 3));
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("[1]", 4));
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("[2]", 5));
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("[3]", 6));

            contextValue.AddValueFromExpression("someExpression",
                RemoteValueFakeUtil.CreateSimpleInt("someVal", 2));

            var provider = new ExpressionNumChildrenProvider("someExpression", contextValue);
            var numChildren = provider.GetNumChildren(remoteValue);

            Assert.That(numChildren, Is.EqualTo(2));
        }

        [Test]
        public void TestGetSpecifier()
        {
            var provider = new ExpressionNumChildrenProvider("someExpression", null);
            Assert.That(provider.Specifier, Is.EqualTo("someExpression"));
        }
    }
}

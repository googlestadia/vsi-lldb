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

﻿using Castle.DynamicProxy;
using NUnit.Framework;
using YetiCommon.CastleAspects;
using YetiCommon.Tests.CastleAspects.TestSupport;

namespace YetiCommon.Tests.CastleAspects
{
    [TestFixture]
    public class DecoratorExtensionsTests
    {
        CallCountAspect countAspect;
        IDecorator decorator;

        [SetUp]
        public void SetUp()
        {
            var proxyGenerator = new ProxyGenerator();
            countAspect = new CallCountAspect();
            decorator = new Decorator(proxyGenerator, countAspect);
        }

        [Test]
        public void DecorateClass()
        {
            var targetObj = new TargetObjImpl();
            var proxy = decorator.Decorate(targetObj);

            Assert.That(proxy, Is.SameAs(targetObj.Self));
            Assert.That(1, Is.EqualTo(proxy.DoSomething()));
            Assert.That(1, Is.EqualTo(countAspect.CallCount));
        }

        [Test]
        public void DecorateInterface()
        {
            ISelfType targetObj = new TargetObjImpl();
            var proxy = decorator.Decorate(targetObj);

            Assert.That(proxy, Is.SameAs(((TargetObjImpl)targetObj).Self));
            Assert.That(1, Is.EqualTo(proxy.DoSomething()));
            Assert.That(1, Is.EqualTo(countAspect.CallCount));
        }

        [Test]
        public void CanDecorateNullObject()
        {
            ISelfType targetObj = null;
            ISelfType proxy = decorator.Decorate(targetObj);

            Assert.That(proxy, Is.Null);
        }
    }
}

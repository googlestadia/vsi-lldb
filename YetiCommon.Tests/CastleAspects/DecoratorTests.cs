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
using System;
using YetiCommon.CastleAspects;
using YetiCommon.Tests.CastleAspects.TestSupport;

namespace YetiCommon.Tests.CastleAspects
{
    [TestFixture]
    class DecoratorTests
    {
        CallCountAspect aspectStub;
        Decorator classUnderTest;

        [SetUp]
        public void SetUp()
        {
            var proxyGenerator = new ProxyGenerator();
            var proxyGenerationOptions = new ProxyGenerationOptions();
            aspectStub = new CallCountAspect();
            classUnderTest = new Decorator(proxyGenerator, proxyGenerationOptions,
                aspectStub);
        }

        [Test]
        public void CanDecorateAClassType()
        {
            var dummyObject = new DummyObject();

            var proxy = (DummyObject)classUnderTest.Decorate(typeof(DummyObject), dummyObject);

            proxy.SetValue(7);
            Assert.AreEqual(1, aspectStub.CallCount);
            proxy.GetValue();
            Assert.AreEqual(2, aspectStub.CallCount);
        }

        [Test]
        public void CanDecorateAnInterfaceType()
        {
            var dummyObjectFactory = new DummyObject.Factory();
            IDummyObject dummyObject = dummyObjectFactory.Create();

            var proxy = (DummyObject)classUnderTest.Decorate(typeof(DummyObject), dummyObject);

            proxy.SetValue(7);
            Assert.AreEqual(1, aspectStub.CallCount);
            proxy.GetValue();
            Assert.AreEqual(2, aspectStub.CallCount);
        }

        [Test]
        public void ShouldThrowWhenDecoratingInvalidTarget()
        {
            var proxyGenerator = new ProxyGenerator();
            var invalidateAllTargets = new ProxyGenerationOptions(new InvalidProxyHook());
            IDecorator classUnderTest = new Decorator(proxyGenerator, invalidateAllTargets,
                aspectStub);
            var dummyObject = new DummyObject();

            Assert.Throws<ArgumentException>(() =>
                classUnderTest.Decorate(typeof(DummyObject), dummyObject));
        }

        [Test]
        public void ShouldThrowWhenAPrimitive()
        {
            Assert.Throws<ArgumentException>(() =>
                classUnderTest.Decorate(typeof(int), 9));
        }

        [Test]
        public void ShouldReturnNullWhenDecoratingNull()
        {
            var proxy =
                (DummyObject)classUnderTest.Decorate(typeof(DummyObject), (DummyObject)null);
            Assert.AreEqual(null, proxy);
        }

        [Test]
        public void TargetObjDoesntImplementProxyType()
        {
            Assert.Throws<ArgumentException>(() =>
                classUnderTest.Decorate(typeof(Exception), new DummyObject()));
        }
    }

    /// <summary>
    /// Integraton tests between Decorator and DecoratorSelfUtil.
    /// </summary>
    [TestFixture]
    public class Decorator_DecorateSelfTests
    {
        CallCountAspect aspectStub;
        IDecorator classUnderTest;

        [SetUp]
        public void SetUp()
        {
            var proxyGenerator = new ProxyGenerator();
            var proxyGenerationOptions = new ProxyGenerationOptions();
            aspectStub = new CallCountAspect();
            classUnderTest = new Decorator(proxyGenerator, proxyGenerationOptions,
                aspectStub);
        }

        [Test]
        public void ShouldSetSelfWhenDecorating()
        {
            var targetObj = new TargetObjImpl();

            var proxy = (TargetObjImpl)classUnderTest.Decorate(typeof(TargetObjImpl), targetObj);

            Assert.AreSame(targetObj.Self, proxy);
        }
    }
}

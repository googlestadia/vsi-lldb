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
using System.Collections.Generic;
using YetiCommon.CastleAspects;
using YetiCommon.Tests.CastleAspects.TestSupport;

namespace YetiCommon.Tests.CastleAspects
{
    [TestFixture]
    class DecoratorUtilTests
    {
        ProxyGenerator proxyGenerator;
        DummyObject.Factory factory;
        DecoratorUtil classUnderTest;

        [SetUp]
        public void SetUp()
        {
            proxyGenerator = new ProxyGenerator();
            factory = new DummyObject.Factory();
            classUnderTest = new DecoratorUtil();
        }

        [Test]
        public void FactoryDecoratorShouldNotModifyObjectWithEmptyAspectsList()
        {
            var factoryDecorator = classUnderTest.CreateFactoryDecorator(
                proxyGenerator);

            var decoratedFactory = factoryDecorator.Decorate(factory);

            Assert.AreSame(factory, decoratedFactory);
        }

        [Test]
        public void FactoryDecoratorShouldInterceptDecoratedFactoryMethods()
        {
            var aspectStub = new CallCountAspect();
            var factoryDecorator = classUnderTest.CreateFactoryDecorator(proxyGenerator,
                aspectStub);

            var decoratedFactory = factoryDecorator.Decorate(factory);
            var proxiedDummyObject = decoratedFactory.Create();

            Assert.AreEqual(0, aspectStub.CallCount);
            proxiedDummyObject.SetValue(7);
            Assert.AreEqual(1, aspectStub.CallCount);
            proxiedDummyObject.GetValue();
            Assert.AreEqual(2, aspectStub.CallCount);
        }

        [Test]
        public void ShouldNotProxyNestedCreateCallsMultipleTimes()
        {
            var aspectStub = new CallCountAspect();
            var factoryDecorator = classUnderTest.CreateFactoryDecorator(proxyGenerator,
                aspectStub);

            var decoratedFactory = factoryDecorator.Decorate(factory);
            var proxiedDummyObject = decoratedFactory.CreateWithNestedCall();

            Assert.AreEqual(0, aspectStub.CallCount);
            proxiedDummyObject.SetValue(7);
            Assert.AreEqual(1, aspectStub.CallCount); ;
        }

        [Test]
        public void ShouldForwardDecoratedFactoryNullReturnValue()
        {
            var aspectStub = new CallCountAspect();
            var factoryDecorator = classUnderTest.CreateFactoryDecorator(proxyGenerator,
                aspectStub);

            var decoratedFactory = factoryDecorator.Decorate(factory);
            var proxiedDummyObject = decoratedFactory.CreateReturnsNull();

            Assert.Null(proxiedDummyObject);
        }

        [Test]
        public void ShouldThrowWhenDecoratingInvalidFactory()
        {
            var aspectStub = new CallCountAspect();
            var factoryDecorator = classUnderTest.CreateFactoryDecorator(proxyGenerator,
                aspectStub);
            var invalidFactory = new DummyObject.FactoryWithVoidCreateMethod();

            Assert.Throws<ArgumentException>(() =>
                factoryDecorator.Decorate(invalidFactory));
        }

        [Test]
        public void ShouldThrowWhenDecoratingInvalidFactoryForValidateOnlyCase()
        {
            var factoryDecorator = classUnderTest.CreateFactoryDecorator(proxyGenerator);
            var invalidFactory = new DummyObject.FactoryWithVoidCreateMethod();

            Assert.Throws<ArgumentException>(() =>
                factoryDecorator.Decorate(invalidFactory));
        }
    }

    [TestFixture]
    public class DecoratorUtil_AspectOrder_Tests
    {
        public class CallOrderAspect : IInterceptor
        {
            int id;
            Action<int> interceptCallback;

            public CallOrderAspect(int id, Action<int> interceptCallback)
            {
                this.id = id;
                this.interceptCallback = interceptCallback;
            }

            public void Intercept(IInvocation invocation)
            {
                interceptCallback(id);
                invocation.Proceed();
            }
        }

        ProxyGenerator proxyGenerator;
        DummyObject.Factory factory;
        DecoratorUtil decoratorUtil;

        [SetUp]
        public void SetUp()
        {
            proxyGenerator = new ProxyGenerator();
            factory = new DummyObject.Factory();
            decoratorUtil = new DecoratorUtil();
        }

        [Test]
        public void AspectOrderIsInvocationOrderOnFactoryCreatedObjects()
        {
            var callOrder = new List<int>();
            var invocationCallback = (Action<int>)((id) =>
            {
                callOrder.Add(id);
            });
            var aspects = new IInterceptor[]
            {
                new CallOrderAspect(0, invocationCallback),
                new CallOrderAspect(1, invocationCallback)
            };
            var factoryDecorator = decoratorUtil.CreateFactoryDecorator(proxyGenerator, aspects);
            var decoratedFactory = factoryDecorator.Decorate(factory);
            var dummyObj = decoratedFactory.Create();

            dummyObj.GetValue();

            Assert.That(callOrder.Count, Is.EqualTo(2));
            Assert.That(callOrder[0], Is.EqualTo(0));
            Assert.That(callOrder[1], Is.EqualTo(1));
        }
    }
}

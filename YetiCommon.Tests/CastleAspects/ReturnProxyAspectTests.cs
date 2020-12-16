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
using NSubstitute;
using NUnit.Framework;
using System;
using YetiCommon.CastleAspects;
using YetiCommon.Tests.CastleAspects.TestSupport;

namespace YetiCommon.Tests.CastleAspects
{
    [TestFixture]
    class ReturnProxyAspectTests
    {
        ProxyGenerator proxyGenerator;
        CallCountAspect aspect;
        IInterceptor classUnderTest;
        IInvocation mockInvocation;

        [SetUp]
        public void SetUp()
        {
            proxyGenerator = new ProxyGenerator();
            aspect = new CallCountAspect();
            classUnderTest = new ReturnProxyAspect(new Decorator(proxyGenerator, aspect));
            mockInvocation = Substitute.For<IInvocation>();
        }

        [Test]
        public void ShouldReturnInterfaceProxy()
        {
            var dummyObjectFactory = new DummyObject.Factory();
            IDummyObject dummyObject = dummyObjectFactory.Create();
            mockInvocation.Method.Returns(
                MethodInfoUtil.GetMethodInfo(() => dummyObjectFactory.Create()));
            mockInvocation.ReturnValue.Returns(dummyObject);

            Assert.AreEqual(dummyObject, mockInvocation.ReturnValue);
            classUnderTest.Intercept(mockInvocation);
            Assert.AreNotEqual(dummyObject, mockInvocation.ReturnValue);

            IDummyObject proxy = mockInvocation.ReturnValue as IDummyObject;
            Assert.NotNull(proxy);
            Assert.AreEqual(0, aspect.CallCount);
            proxy.SetValue(0);
            Assert.AreEqual(1, aspect.CallCount);
        }

        [Test]
        public void ShouldForwardNullReturnValueOnInterfaceFunction()
        {
            mockInvocation.Method.Returns(
                MethodInfoUtil.GetMethodInfo<DummyObject.Factory>(factory => factory.Create()));
            mockInvocation.ReturnValue.Returns(null);

            classUnderTest.Intercept(mockInvocation);
            Assert.Null(mockInvocation.ReturnValue);
        }

        [Test]
        public void ShouldReturnClassProxy()
        {
            var dummyObjectFactory = new DummyObject.FactoryWithConcreteCreate();
            IDummyObject dummyObject = dummyObjectFactory.Create();
            mockInvocation.Method.Returns(
                MethodInfoUtil.GetMethodInfo<DummyObject.FactoryWithConcreteCreate>(
                    factory => factory.CreateConcrete()));
            mockInvocation.ReturnValue.Returns(dummyObject);

            Assert.AreEqual(dummyObject, mockInvocation.ReturnValue);
            classUnderTest.Intercept(mockInvocation);
            Assert.AreNotEqual(dummyObject, mockInvocation.ReturnValue);

            IDummyObject proxy = mockInvocation.ReturnValue as IDummyObject;
            Assert.NotNull(proxy);
            Assert.AreEqual(0, aspect.CallCount);
            proxy.SetValue(0);
            Assert.AreEqual(1, aspect.CallCount);
        }

        [Test]
        public void ShouldThrowInvalidOperationWhenProxyingVoidFunction()
        {
            mockInvocation.Method.Returns(
                MethodInfoUtil.GetMethodInfo<DummyObject.FactoryWithVoidCreateMethod>(
                    factory => factory.CreateNothing()));

            var exceptionThrown = Assert.Throws(typeof(InvalidOperationException), () =>
            {
                classUnderTest.Intercept(mockInvocation);
            });
            Assert.That(exceptionThrown.Message, Does.Contain("System.Void"));
        }

        [Test]
        public void ShouldThrowInvalidOperationWhenProxyingPrimativeReturnTypeFunction()
        {
            mockInvocation.Method.Returns(
                MethodInfoUtil.GetMethodInfo<DummyObject.FactoryWithCreateReturnType<int>>(
                    factory => factory.CreateWithGivenReturnType()));

            var exceptionThrown = Assert.Throws(typeof(InvalidOperationException), () =>
            {
                classUnderTest.Intercept(mockInvocation);
            });
            Assert.That(exceptionThrown.Message, Does.Contain("System.Int32"));
        }
    }

    /// <summary>
    /// Integraton tests between ReturnProxyAspect and DecoratorSelfUtil/DecoratorSelfUtil.
    /// </summary>
    [TestFixture]
    public class ReturnProxyAspect_DecoratorSelfTests
    {
        ProxyGenerator proxyGenerator;
        CallCountAspect aspect;
        IInterceptor classUnderTest;
        IInvocation mockInvocation;

        [SetUp]
        public void SetUp()
        {
            proxyGenerator = new ProxyGenerator();
            aspect = new CallCountAspect();
            classUnderTest = new ReturnProxyAspect(new Decorator(proxyGenerator, aspect));
            mockInvocation = Substitute.For<IInvocation>();
        }

        public class Factory
        {
            public ISelfType Create()
            {
                return new TargetObjImpl();
            }
        }

        [Test]
        public void TargetSelfIsSetToProxy()
        {
            var factory = new Factory();
            var targetObj = factory.Create();
            mockInvocation.Method.Returns(
                MethodInfoUtil.GetMethodInfo(() => factory.Create()));
            mockInvocation.ReturnValue.Returns(targetObj);

            Assert.AreEqual(targetObj, mockInvocation.ReturnValue);
            classUnderTest.Intercept(mockInvocation);
            Assert.AreNotEqual(targetObj, mockInvocation.ReturnValue);
            Assert.AreSame(((TargetObjImpl)targetObj).Self, mockInvocation.ReturnValue);
        }
    }
}


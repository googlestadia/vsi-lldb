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
using System.Reflection;
using YetiCommon.CastleAspects;
using YetiCommon.Tests.CastleAspects.TestSupport;

namespace YetiCommon.Tests.CastleAspects
{
    [TestFixture]
    public class CreateFunctionProxyHookTests
    {
        #region Helpers

        class SubClassCreateFunctionProxyHook : CreateFunctionProxyHook
        {
        }

        /// <summary>
        /// Helper function to generate proxy objects with the CreateFunctionProxyHook option.
        /// </summary>
        private object GenerateClassProxyForCreateMethods(Type type, object target)
        {
            var proxyGenerator = new ProxyGenerator();
            return proxyGenerator.CreateClassProxyWithTarget(type,
                target,
                new ProxyGenerationOptions(new CreateFunctionProxyHook()),
                new CallCountAspect());
        }

        /// <summary>
        /// Helper function to generate proxy objects with the CreateFunctionProxyHook option.
        /// </summary>
        private object GenerateInterfaceProxyForCreateMethods(Type type)
        {
            var proxyGenerator = new ProxyGenerator();
            return proxyGenerator.CreateInterfaceProxyWithoutTarget(type,
                new ProxyGenerationOptions(new CreateFunctionProxyHook()),
                new CallCountAspect());
        }

        public interface UnconventionalFactory
        {
            /// <summary>
            /// Doesn't start with 'Create' so it requires the ShouldInterceptAttribute to be
            /// intercepted.
            /// </summary>
            [CreateFunctionProxyHook.ShouldIntercept]
            UnconventionalFactory ValidCreate();

            /// <summary>
            /// Doesn't start with 'Create' so it should not be intercepted.
            /// </summary>
            UnconventionalFactory InvalidCreate();
        }

        #endregion

        [Test]
        public void ShouldInterceptVirtualCreateMethodWithInterfaceReturnType()
        {
            var sut = new CreateFunctionProxyHook();
            Assert.True(sut.ShouldInterceptMethod(
                typeof(DummyObject.Factory),
                MethodInfoUtil.GetMethodInfo<DummyObject.Factory>(
                    factory => factory.Create()
            )));
        }

        [Test]
        public void ShouldThrowArgumentNullExceptionForNullType()
        {
            var exceptionThrown = Assert.Throws<ArgumentNullException>(() =>
            {
                var sut = new CreateFunctionProxyHook();
                sut.ShouldInterceptMethod((Type)null, default(MethodInfo));
            });
            Assert.That(exceptionThrown.Message, Does.Contain("type"));
        }

        [Test]
        public void ShouldThrowArgumentNullExceptionForNullMethodInfo()
        {
            var exceptionThrown = Assert.Throws<ArgumentNullException>(() =>
            {
                var sut = new CreateFunctionProxyHook();
                sut.ShouldInterceptMethod(typeof(object), null);
            });
            Assert.That(exceptionThrown.Message, Does.Contain("methodInfo"));
        }

        [Test]
        public void ShouldThrowArgumentExceptionForVoidCreateMethod()
        {
            var exceptionThrown = Assert.Throws<ArgumentException>(() =>
            {
                var sut = new CreateFunctionProxyHook();
                sut.ShouldInterceptMethod(typeof(DummyObject.FactoryWithVoidCreateMethod),
                    MethodInfoUtil.GetMethodInfo<DummyObject.FactoryWithVoidCreateMethod>(
                        factory => factory.CreateNothing()
                ));
            });
            Assert.That(exceptionThrown.Message, Does.Contain("System.Void"));
        }

        [Test]
        public void ShouldThrowArgumentExceptionForClassCreateMethod()
        {
            var exceptionThrown = Assert.Throws<ArgumentException>(() =>
            {
                var sut = new CreateFunctionProxyHook();
                sut.ShouldInterceptMethod(typeof(DummyObject.FactoryWithConcreteCreate),
                    MethodInfoUtil.GetMethodInfo<DummyObject.FactoryWithConcreteCreate>(
                        factory => factory.CreateConcrete()
                ));
            });
            Assert.That(exceptionThrown.Message, Does.Contain("DummyObject"));
        }

        [Test]
        public void ShouldThrowArgumentExceptionForPrimitiveCreateMethod()
        {
            var exceptionThrown = Assert.Throws<ArgumentException>(() =>
            {
                var sut = new CreateFunctionProxyHook();
                sut.ShouldInterceptMethod(typeof(DummyObject.FactoryWithCreateReturnType<int>),
                    MethodInfoUtil.GetMethodInfo<DummyObject.FactoryWithCreateReturnType<int>>(
                        factory => factory.CreateWithGivenReturnType()
                ));
            });
            Assert.That(exceptionThrown.Message, Does.Contain("System.Int32"));
        }

        [Test]
        public void FailToGenerateProxyWithNonVirtualMethod()
        {
            Assert.Throws<ArgumentException>(() =>
                GenerateClassProxyForCreateMethods(
                    typeof(DummyObject.FactoryWithNonVirtualMethod),
                    new DummyObject.FactoryWithNonVirtualMethod()));
        }

        [Test]
        public void FailToGenerateProxyWithNonVirtualProperty()
        {
            Assert.Throws<ArgumentException>(() =>
                GenerateClassProxyForCreateMethods(
                    typeof(DummyObject.FactoryWithPublicNonVirtualProperty),
                    new DummyObject.FactoryWithPublicNonVirtualProperty()));
        }

        [Test]
        public void FailToGenerateProxyWithNonVirtualEvent()
        {
            Assert.Throws<ArgumentException>(() =>
                GenerateClassProxyForCreateMethods(
                    typeof(DummyObject.FactoryWithPublicNonVirtualEvent),
                    new DummyObject.FactoryWithPublicNonVirtualEvent()));
        }

        [Test]
        public void FailToGenerateProxyWithVoidReturnType()
        {
            var exceptionThrown = Assert.Throws<ArgumentException>(() =>
                GenerateClassProxyForCreateMethods(typeof(DummyObject.FactoryWithVoidCreateMethod),
                new DummyObject.FactoryWithVoidCreateMethod())
            );
            Assert.That(exceptionThrown.Message, Does.Contain("System.Void"));
        }

        [Test]
        public void FailToGenerateProxyWithPrimitiveReturnType()
        {
            var exceptionThrown = Assert.Throws<ArgumentException>(() =>
                GenerateClassProxyForCreateMethods(
                    typeof(DummyObject.FactoryWithCreateReturnType<int>),
                    new DummyObject.FactoryWithCreateReturnType<int>())
            );
            Assert.That(exceptionThrown.Message, Does.Contain("System.Int"));
        }

        [Test]
        public void FailToGenerateProxyWithClassReturnType()
        {
            var exceptionThrown = Assert.Throws<ArgumentException>(() =>
                GenerateClassProxyForCreateMethods(
                    typeof(DummyObject.FactoryWithCreateReturnType<System.Object>),
                    new DummyObject.FactoryWithCreateReturnType<System.Object>())
            );
            Assert.That(exceptionThrown.Message, Does.Contain("System.Object"));
        }

        [Test]
        public void SucceedToCreateWithVirtualMembers()
        {
            Assert.DoesNotThrow(() =>
            {
                GenerateClassProxyForCreateMethods(
                    typeof(DummyObject.FactoryWithVirtualMembers),
                    new DummyObject.FactoryWithVirtualMembers());
            });
        }

        [Test]
        public void SucceedToCreateWithPrivateMembers()
        {
            Assert.DoesNotThrow(() =>
            {
                GenerateClassProxyForCreateMethods(
                    typeof(DummyObject.FactoryWithPrivateMembers),
                    new DummyObject.FactoryWithPrivateMembers());
            });
        }

        [Test]
        public void SucceedToCreateWithInterfaceReturnType()
        {
            Assert.DoesNotThrow(() =>
            {
                GenerateClassProxyForCreateMethods(
                    typeof(DummyObject.FactoryWithCreateReturnType<IDummyObject>),
                    new DummyObject.FactoryWithCreateReturnType<IDummyObject>());
            });
        }

        [Test]
        public void ValidUnconventionalFactoryShouldIntercept()
        {
            var sut = new CreateFunctionProxyHook();
            Assert.True(sut.ShouldInterceptMethod(
                typeof(UnconventionalFactory),
                MethodInfoUtil.GetMethodInfo<UnconventionalFactory>(
                    factory => factory.ValidCreate()
            )));
        }

        [Test]
        public void InvalidUnconventionalFactoryShouldFail()
        {
            var sut = new CreateFunctionProxyHook();
            var exceptionThrown = Assert.Throws<ArgumentException>(() =>
                sut.ShouldInterceptMethod(
                    typeof(UnconventionalFactory),
                    MethodInfoUtil.GetMethodInfo<UnconventionalFactory>(
                        factory => factory.InvalidCreate()
            )));
            Assert.That(exceptionThrown.Message,
                Does.Contain(nameof(CreateFunctionProxyHook.ShouldInterceptAttribute)));
        }
    }
}

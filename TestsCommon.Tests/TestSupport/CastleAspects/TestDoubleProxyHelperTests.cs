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

using Castle.DynamicProxy;
using NUnit.Framework;
using System;
using TestsCommon.TestSupport.CastleAspects;

namespace TestsCommon.Tests.TestSupport.CastleAspects
{
    [TestFixture]
    public class TestDoubleProxyHelperTests
    {
        /// <summary>
        ///  Dummy interface to use by test.
        /// </summary>
        public interface ITestTarget
        {
            void VirtualInterfaceMethod1();

            void VirtualInterfaceMethod2();

            void NonVirtualInterfaceMethod();
        }

        /// <summary>
        ///  Dummy class to use by test.
        /// </summary>
        public class TestTarget : ITestTarget
        {
            public virtual void VirtualInterfaceMethod1() { }

            public virtual void VirtualInterfaceMethod2() { }

            public void NonVirtualInterfaceMethod() { }

            public void NonVirtualMethod() { }

            public virtual void VirtualMethod1() { }

            public virtual void VirtualMethod2() { }
        }

        TestDoubleProxyHelper proxyHelper;

        [SetUp]
        public void SetUp()
        {
            proxyHelper = new TestDoubleProxyHelper(new ProxyGenerator());
        }

        [Test]

        public void NullMethodNameThrowsNotSupportedException()
        {
            ITestTarget targetClass = new TestTarget();
            Assert.Throws<ArgumentNullException>(() => proxyHelper.DoNotCall(targetClass, null));
        }

        [Test]
        public void InterfaceTargetInterfaceMethods()
        {
            ITestTarget targetInterface = new TestTarget();
            targetInterface = proxyHelper.DoNotCall<ITestTarget>(
                targetInterface, nameof(ITestTarget.VirtualInterfaceMethod1));

            Assert.Throws<DoNotCallException>(() => targetInterface.VirtualInterfaceMethod1());

            // Verifies no exception raised.
            targetInterface.VirtualInterfaceMethod2();
        }

        [Test]
        public void ClassTargetInterfaceMethods()
        {
            TestTarget targetClass = new TestTarget();
            targetClass = proxyHelper.DoNotCall<TestTarget>(
                targetClass, nameof(ITestTarget.VirtualInterfaceMethod1));

            Assert.Throws<DoNotCallException>(() => targetClass.VirtualInterfaceMethod1());

            // Verifies no exception raised.
            targetClass.VirtualInterfaceMethod2();
        }

        [Test]
        public void ClassTargetNonVirtualInterfaceMethod()
        {
            TestTarget targetClass = new TestTarget();
            Assert.Throws<NotSupportedException>(() => proxyHelper.DoNotCall<TestTarget>(
                targetClass, nameof(ITestTarget.NonVirtualInterfaceMethod)));
        }

        [Test]
        public void ClassTargetNonVirtualMethodThrowsNotSupportedException()
        {
            TestTarget targetClass = new TestTarget();
            Assert.Throws<NotSupportedException>(() => proxyHelper.DoNotCall(
                targetClass, nameof(TestTarget.NonVirtualMethod)));
        }

        [Test]
        public void ClassTargetVirtualMethods()
        {
            TestTarget targetClass = new TestTarget();
            targetClass = proxyHelper.DoNotCall<TestTarget>(
                targetClass, nameof(TestTarget.VirtualMethod1));

            Assert.Throws<DoNotCallException>(() => targetClass.VirtualMethod1());

            // Verifies no exception raised.
            targetClass.VirtualMethod2();
        }
    }
}

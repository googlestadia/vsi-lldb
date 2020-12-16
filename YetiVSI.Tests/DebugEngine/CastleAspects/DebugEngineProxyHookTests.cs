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
using YetiCommon.Tests.CastleAspects.TestSupport;
using YetiVSI.DebugEngine.CastleAspects;
using Castle.DynamicProxy;
using Interop;

namespace Interop
{
    public interface API_Base_1
    {
        void Interop_Method_WithOutArg(out int val);

        void Interop_Method_WithRefArg(ref int val);

        void Interop_Method_1();
    }

    public interface IDerivedAPI : API_Base_1
    {
        void Interop_DerivedMethod();
    }

    public interface API_Base_2
    {
        void Interop_Method_2();
    }
}

namespace YetiVSI.Test.DebugEngine.CastleAspects
{
    public interface IDomainObj : IDerivedAPI, API_Base_2
    {
        void Domain_InterfaceMethod();

        [InteropBoundary]
        void Synthetic_InterfaceInteropMethod();
    }

    public class ConcreteObj : IDomainObj
    {
        public virtual void Interop_Method_WithOutArg(out int val)
        {
            val = 23;
        }

        public virtual void Interop_Method_WithRefArg(ref int val)
        {
            val = 17;
        }

        public virtual void Interop_Method_1() { }

        public virtual void Interop_DerivedMethod() { }

        public virtual void Interop_Method_2() { }

        public virtual void Domain_InterfaceMethod() { }

        public virtual void Domain_ClassMethod() { }

        public virtual void Synthetic_InterfaceInteropMethod() { }

        [InteropBoundary]
        public virtual void Synthetic_ClassInteropMethod() { }
    }

    public class DebugEngineProxyHookTests
    {
        /// <summary>
        /// Common test cases that apply to all types deriving from IDomainObj.
        /// </summary>
        public abstract class Common<T> where T : IDomainObj
        {
            protected ProxyGenerator proxyGen;
            protected CallCountAspect callCountAspect;

            protected T proxy;

            [SetUp]
            public void SetUp()
            {
                proxyGen = new ProxyGenerator();
                callCountAspect = new CallCountAspect();
                proxy = CreateProxy();
            }

            protected abstract T CreateProxy();

            [Test]
            public void InteropMethodIntercepted_1()
            {
                ((API_Base_1)proxy).Interop_Method_1();
                Assert.That(callCountAspect.CallCount, Is.EqualTo(1));
            }

            [Test]
            public void InteropMethodIntercepted_Derived()
            {
                ((IDerivedAPI)proxy).Interop_DerivedMethod();
                Assert.That(callCountAspect.CallCount, Is.EqualTo(1));
            }

            [Test]
            public void InteropMethodIntercepted_2()
            {
                ((API_Base_2)proxy).Interop_Method_2();
                Assert.That(callCountAspect.CallCount, Is.EqualTo(1));
            }

            [Test]
            public void InteropMethodWithRefArgNotIntercepted()
            {
                int val = -1;

                proxy.Interop_Method_WithRefArg(ref val);
                Assert.That(callCountAspect.CallCount, Is.EqualTo(0));
            }

            [Test]
            public void InteropMethodWithOutArgIntercepted()
            {
                int val = -1;

                proxy.Interop_Method_WithOutArg(out val);
                Assert.That(callCountAspect.CallCount, Is.EqualTo(1));
            }

            [Test]
            public void DomainInterfaceMethodIsNotIntercepted()
            {
                proxy.Domain_InterfaceMethod();
                Assert.That(callCountAspect.CallCount, Is.EqualTo(0));
            }
        }

        /// <summary>
        /// Tests interface based proxies
        /// </summary>
        [TestFixture]
        public class DomainInterfaceProxy : Common<IDomainObj>
        {
            [Test]
            public void SyntheticInterfaceMethodIsIntercepted()
            {
                proxy.Synthetic_InterfaceInteropMethod();
                Assert.That(callCountAspect.CallCount, Is.EqualTo(1));
            }

            protected override IDomainObj CreateProxy()
            {
                return (IDomainObj)proxyGen.CreateInterfaceProxyWithTarget(
                    typeof(IDomainObj),
                    new ConcreteObj(),
                    new ProxyGenerationOptions(
                        DebugEngineProxyHook.CreateForTest(typeof(API_Base_1).Namespace)),
                    callCountAspect);
            }
        }

        /// <summary>
        /// Tests class based proxies.
        /// </summary>
        [TestFixture]
        public class DomainClassProxy : Common<ConcreteObj>
        {
            protected override ConcreteObj CreateProxy()
            {
                var debugEngine = new ConcreteObj();
                debugEngine = (ConcreteObj)proxyGen.CreateClassProxyWithTarget(
                    typeof(ConcreteObj),
                    debugEngine,
                    new ProxyGenerationOptions(
                        DebugEngineProxyHook.CreateForTest(typeof(API_Base_1).Namespace)),
                    callCountAspect);
                return debugEngine;
            }

            [Test]
            public void DomainClassMethodIsNotIntercepted()
            {
                proxy.Domain_ClassMethod();
                Assert.That(callCountAspect.CallCount, Is.EqualTo(0));
            }

            [Test]
            public void SyntheticInterfaceMethodIsNotIntercepted()
            {
                proxy.Synthetic_InterfaceInteropMethod();
                Assert.That(callCountAspect.CallCount, Is.EqualTo(0));
            }

            [Test]
            public void SyntheticClassMethodIsIntercepted()
            {
                proxy.Synthetic_ClassInteropMethod();
                Assert.That(callCountAspect.CallCount, Is.EqualTo(1));
            }
        }

        /// <summary>
        /// Verify behavior when the proxy type _is_ an Interop type.
        /// </summary>
        [TestFixture]
        public class InteropInterfaceProxy
        {
            protected ProxyGenerator proxyGen;
            protected CallCountAspect callCountAspect;

            [SetUp]
            public void SetUp()
            {
                proxyGen = new ProxyGenerator();
                callCountAspect = new CallCountAspect();
            }

            [Test]
            public void InteropMethodIntercepted_1()
            {
                var proxy = CreateProxy<IDerivedAPI>();

                proxy.Interop_Method_1();
                Assert.That(callCountAspect.CallCount, Is.EqualTo(1));
            }

            [Test]
            public void InteropMethodIntercepted_Derived()
            {
                var proxy = CreateProxy<IDerivedAPI>();

                proxy.Interop_DerivedMethod();
                Assert.That(callCountAspect.CallCount, Is.EqualTo(1));
            }

            [Test]
            public void InteropMethodIntercepted_2()
            {
                var proxy = CreateProxy<API_Base_2>();

                proxy.Interop_Method_2();
                Assert.That(callCountAspect.CallCount, Is.EqualTo(1));
            }

            private T CreateProxy<T>()
            {
                var debugEngine = (T)proxyGen.CreateInterfaceProxyWithTarget(
                    typeof(T),
                    new ConcreteObj(),
                    new ProxyGenerationOptions(
                        DebugEngineProxyHook.CreateForTest(typeof(API_Base_1).Namespace)),
                    callCountAspect);
                return debugEngine;
            }
        }
    }
}

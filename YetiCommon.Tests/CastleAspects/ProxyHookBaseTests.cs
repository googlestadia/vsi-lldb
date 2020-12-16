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
using System;
using YetiCommon.CastleAspects;
using System.Reflection;
using NSubstitute;

namespace YetiCommon.Tests.CastleAspects
{
    [TestFixture]
    class ProxyHookBaseTests
    {
        /// <summary>
        /// Test specific subclass (TSS) required because ProxyHookBase is an abstract class.
        /// </summary>
        class ProxyHookBase_TSS : ProxyHookBase
        {
            public override bool ShouldInterceptMethod(Type type, MethodInfo methodInfo)
            {
                return true;
            }
        }

        /// <summary>
        /// Test specific subclass (TSS) required because ProxyHookBase is an abstract class.
        /// </summary>
        class ProxyHookBase_TSS2 : ProxyHookBase
        {
            public override bool ShouldInterceptMethod(Type type, MethodInfo methodInfo)
            {
                return true;
            }
        }

        /// <summary>
        /// Used for comparison tests between derived instances and the parent type.
        /// </summary>
        class ProxyHookBase_TSS_Derived : ProxyHookBase_TSS
        {
            public override bool ShouldInterceptMethod(Type type, MethodInfo methodInfo)
            {
                return true;
            }
        }

        [Test]
        public void HooksShouldBeEqualWithOtherInstances()
        {
            var hook1 = new ProxyHookBase_TSS();
            var hook2 = new ProxyHookBase_TSS();

            Assert.True(hook1.Equals(hook1));
            Assert.True(hook1.Equals(hook2));
            Assert.True(hook2.Equals(hook1));
        }

        [Test]
        public void HookShouldNotBeEqualWithOtherHookTypes()
        {
            var hook = new ProxyHookBase_TSS();
            var derivedHook = new ProxyHookBase_TSS2();
            var otherHook = Substitute.For<ProxyHookBase>();

            Assert.False(hook.Equals(otherHook));
            Assert.False(hook.Equals(null));
            Assert.False(hook.Equals(derivedHook));
        }

        [Test]
        public void HookHashCodesShouldBeEqual()
        {
            var hook1 = new ProxyHookBase_TSS();
            var hook2 = new ProxyHookBase_TSS();

            Assert.True(hook1.GetHashCode().Equals(hook1.GetHashCode()));
            Assert.True(hook1.GetHashCode().Equals(hook2.GetHashCode()));
        }

        [Test]
        public void HookHashCodeShouldBeDifferentFromDerivedClasses()
        {
            var hook = new ProxyHookBase_TSS();
            var derivedHook = new ProxyHookBase_TSS_Derived();

            Assert.False(hook.GetHashCode().Equals(derivedHook.GetHashCode()));
            Assert.False(derivedHook.GetHashCode().Equals(hook.GetHashCode()));
        }
    }
}

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
using System;
using YetiCommon.CastleAspects;
using System.Reflection;

namespace TestsCommon.TestSupport
{
    /// <summary>
    /// Creates test double dummies.
    ///
    /// Generated test doubles will throw a TestDummyException if any method or property is used.
    /// Supports interface and class target types but only virtual class methods will throw
    /// exceptions.
    /// </summary>
    public static class TestDummyGenerator
    {
        /// <summary>
        /// Used to create test double dummies.
        /// </summary>
        class TestDummyAspect : ThrowExceptionAspect<TestDummyException> { }

        class TestDummyProxyHook : ProxyHookBase
        {
            #region ProxyHookBase

            /// <summary>
            /// Inspects the non-proxyable members. We choose to be more restrictive initially and
            /// enforce that all method, property or event members should be marked as virtual in
            /// an attempt to avoid subtle, unexpected behaviour where a TestDummy could be used
            /// without it throwing a TestDummyException.
            /// </summary>
            /// <exception cref="ArgumentException">Thrown if method, property or event is not
            /// proxyable. For example, they are not virtual when using class proxy.
            /// </exception>
            public override void NonProxyableMemberNotification(Type type, MemberInfo memberInfo)
            {
                if (memberInfo == null) { throw new ArgumentNullException(nameof(memberInfo)); }

                if ((memberInfo.MemberType &
                    (MemberTypes.Method | MemberTypes.Property | MemberTypes.Event)) > 0)
                {
                    throw new ArgumentException($"Member '{memberInfo.Name}' is not " +
                        "proxyable. Consider creating an interface proxy or making it " +
                        " virtual'.");
                }
            }

            public override bool ShouldInterceptMethod(Type type, MethodInfo methodInfo)
            {
                return true;
            }

            #endregion
        }

        public static T Create<T>() where T : class
        {
            var generator = new ProxyGenerator();

            if (typeof(T).IsClass)
            {
                return generator.CreateClassProxy<T>(
                    new ProxyGenerationOptions(new TestDummyProxyHook()), new TestDummyAspect());
            }
            else if (typeof(T).IsInterface)
            {
                return generator.CreateInterfaceProxyWithoutTarget<T>(
                    new ProxyGenerationOptions(new TestDummyProxyHook()), new TestDummyAspect());
            }
            else
            {
                throw new Exception("Internal Error. T should be a class or an interface.");
            }
        }
    }
}

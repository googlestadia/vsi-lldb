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

﻿using System;
using System.Collections.Generic;
using System.Reflection;

namespace YetiCommon.CastleAspects
{
    /// <summary>
    /// Method interceptor filter that white lists methods that start with "Create".
    /// </summary>
    /// <remarks>
    /// Properties will not be intercepted.
    ///
    /// If a method starts with "Create" but has a void or primitive return type then a runtime
    /// exception is raised when generating the proxy.
    ///
    /// If a method doesn't start with "Create" a runtime exception is raised, unless that method
    /// has the ShouldInterceptAttribute applied.
    ///
    /// The following common methods are NOT intercepted:
    ///   - ToString
    ///   - GetHashCode
    ///   - Equals
    /// </remarks>
    public class CreateFunctionProxyHook : ProxyHookBase
    {
        /// <summary>
        /// Explicitly indicate that a method should be within the pointcut defined by the
        /// CreateFunctionProxyHook.
        /// </summary>
        [AttributeUsage(AttributeTargets.Method, Inherited = true)]
        public class ShouldInterceptAttribute : Attribute { }

        // Common methods that should not be intercepted.
        private HashSet<string> ignoredMethods = new HashSet<string>()
        {
            "ToString", "GetHashCode", "Equals"
        };

        #region ProxyHookBase

        /// <summary>
        /// Inspects the non-proxyable members. We choose to enforce that all method, property or
        /// event members should be marked as virtual to avoid unintended consequences of having
        /// non-proxied members acting on the proxy's data instead of the proxied object's data.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if method, property or event is not
        /// proxyable. For example, they are not virtual when using class proxy.
        /// </exception>
        public override void NonProxyableMemberNotification(Type type, MemberInfo memberInfo)
        {
            if (memberInfo == null) { throw new ArgumentNullException(nameof(memberInfo)); }

            if ((memberInfo.MemberType & MemberTypes.Method) > 0)
            {
                throw new ArgumentException($"Member '{memberInfo.Name}' is not " +
                    "proxyable. Consider creating an interface proxy or making it " +
                    " virtual'.");
            }
        }

        /// <summary>
        /// Determines if a method should be intercepted. Methods should be proxyable, prefixed
        /// with "Create", and return an interface.
        /// </summary>
        /// <returns>true if the method should be intercepted.</returns>
        /// <exception cref="ArgumentException">If methodInfo is expected to be proxyable but
        /// isn't. e.g. Create method with 'void' return type.</exception>
        public override bool ShouldInterceptMethod(Type type, MethodInfo methodInfo)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (methodInfo == null)
            {
                throw new ArgumentNullException(nameof(methodInfo));
            }

            // Ignore properties and their method equivalents (i.e. 'get_Property' and
            // 'set_Property') which have IsSpecialName == true.  This also includes other things
            // like overloaded operators.
            if (methodInfo.IsSpecialName ||
                (methodInfo.MemberType & MemberTypes.Property) != 0 ||
                (methodInfo.MemberType & MemberTypes.Event) != 0 ||
                ignoredMethods.Contains(methodInfo.Name))
            {
                return false;
            }

            if (!methodInfo.Name.StartsWith("Create") &&
                !Attribute.IsDefined(methodInfo, typeof(ShouldInterceptAttribute), true))
            {
                throw new ArgumentException(
                    $"Member '{methodInfo.Name}' is proxyable but does not start with " +
                    $"'Create'.  If you wish want to mark it to be intercepted you can rename it " +
                    $"or use the {nameof(ShouldInterceptAttribute)} attribute.");
            }

            if (!methodInfo.ReturnType.IsInterface)
            {
                // Class-based proxies aren't supported because unlike Interface proxies,
                // they require intercepted methods to be virtual and can only catch
                // non-proxyable members at runtime. We opted to be more restrictive initially
                // but it may become helpful to support Class-based proxies.
                throw new ArgumentException("Return type must be an interface. " +
                    "Return type specified " + methodInfo.ReturnType.FullName);
            }
            return true;
        }

        #endregion
    }
}

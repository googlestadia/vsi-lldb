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
using System.Reflection;
using Castle.DynamicProxy;

namespace YetiCommon.CastleAspects
{
    /// <summary>
    /// Proxy hook base class that ensures common functions are implemented properly.
    /// e.g. Equals(), GetHashCode().
    /// </summary>
    public abstract class ProxyHookBase : IProxyGenerationHook
    {
        public override bool Equals(object obj)
        {
            // Equals is used by DynamicProxy for caching return values.
            return (obj != null && GetType() == obj.GetType());
        }

        public override int GetHashCode()
        {
            return GetType().GetHashCode();
        }

        #region IProxyGenerationHook

        /// <summary>
        /// Invoked by the generation process to notify that the whole process has completed.
        /// CreateFunctionProxyHook does not have any clean up steps, so this implementation
        /// is empty.
        /// </summary>
        public virtual void MethodsInspected() { }

        /// <summary>
        /// Inspects the non-proxyable members.
        /// </summary>
        public virtual void NonProxyableMemberNotification(Type type, MemberInfo memberInfo) { }

        /// <summary>
        /// Determines if a method should be intercepted.
        /// </summary>
        /// <returns>true if the method should be interecepted.</returns>
        public abstract bool ShouldInterceptMethod(Type type, MethodInfo methodInfo);

        #endregion
    }
}

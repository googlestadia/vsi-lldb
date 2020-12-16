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

using System;
using System.Reflection;

namespace YetiCommon.CastleAspects
{
    /// <summary>
    /// Filter methods matching a name to be intercepted.
    ///
    /// If a non proxyable method is found to match, a NotSupportedException will be thrown.
    /// </summary>
    public class MethodInfoProxyHook : ProxyHookBase
    {
        private string methodName;

        /// <exception cref="ArgumentNullException">When methodName is null.</exception>
        public MethodInfoProxyHook(string methodName)
        {
            if (methodName == null) { throw new ArgumentNullException(nameof(methodName)); }
            this.methodName = methodName;
        }

        public override void NonProxyableMemberNotification(Type type, MemberInfo memberInfo)
        {
            if (GetMethod(type).Equals(memberInfo))
            {
                throw new NotSupportedException("Only virtual methods can be intercepted.");
            }
            base.NonProxyableMemberNotification(type, memberInfo);
        }

        public override bool ShouldInterceptMethod(Type type, MethodInfo methodInfo)
        {
            var foundMethod = GetMethod(type);
            if (foundMethod == null)
            {
                return false;
            }
            return foundMethod.Equals(methodInfo);
        }

        private MethodInfo GetMethod(Type type)
        {
            return type.GetMethod(methodName);
        }
    }
}

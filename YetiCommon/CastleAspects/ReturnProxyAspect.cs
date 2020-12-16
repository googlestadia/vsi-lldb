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

namespace YetiCommon.CastleAspects
{
    /// <summary>
    /// Applies Decorator.Decorate() to the return value of the intercepted method.
    ///
    /// Return types must be a class or interface type.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Raise if the return type of the method is not a class or interface type.
    /// </exception>
    public class ReturnProxyAspect : IInterceptor
    {
        Decorator decorator;

        public ReturnProxyAspect(Decorator decorator)
        {
            this.decorator = decorator;
        }

        #region IInterceptor

        public void Intercept(IInvocation invocation)
        {
            var methodInfo = invocation.Method;
            if (!(methodInfo.ReturnType.IsInterface || methodInfo.ReturnType.IsClass))
            {
                throw new InvalidOperationException(
                    "Expected class or interface return type but encountered a return type of " +
                    $"'{methodInfo.ReturnType}'.");
            }

            invocation.Proceed();
            if (invocation.ReturnValue == null)
            {
                return;
            }

            invocation.ReturnValue =
                decorator.Decorate(methodInfo.ReturnType, invocation.ReturnValue);
        }

        #endregion
    }
}

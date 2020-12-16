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
using System.Linq.Expressions;
using System.Reflection;

namespace YetiCommon.Tests.CastleAspects.TestSupport
{
    /// <summary>
    /// This class exists to fetch MethodInfo objects in testing.
    /// </summary>
    public static class MethodInfoUtil
    {
        /// <summary>
        /// Get the MethodInfo for a method called in the body of the lambda expression.
        /// </summary>
        /// <param name="expr">
        /// Lambda expression with a single method call in the body
        /// e.g. factory => factory.Create()
        /// </param>
        /// <typeparam name="T">
        /// The class or interface that the method is a part of.
        /// </typeparam>
        public static MethodInfo GetMethodInfo<T>(Expression<Action<T>> expr)
        {
            return GetMethodInfo((LambdaExpression)expr);
        }

        /// <summary>
        /// Get the MethodInfo for a method called in the body of the lambda expression.
        /// </summary>
        /// <param name="expr">
        /// Lambda expression with a single method call in the body
        /// e.g. () => Trace.WriteLine()
        /// </param>
        public static MethodInfo GetMethodInfo(Expression<Action> expr)
        {
            return GetMethodInfo((LambdaExpression)expr);
        }

        /// <summary>
        /// Get the MethodInfo for a method called in the body of the lambda expression.
        /// </summary>
        public static MethodInfo GetMethodInfo(LambdaExpression expr)
        {
            var body = expr.Body as MethodCallExpression;
            if (body != null)
            {
                return body.Method;
            }
            throw new ArgumentException(
                "Expected lambda body to be a single method call.");
        }
    }
}

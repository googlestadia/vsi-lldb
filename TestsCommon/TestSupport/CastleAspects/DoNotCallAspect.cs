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
using System;

namespace TestsCommon.TestSupport.CastleAspects
{
    /// <summary>
    /// Thrown when a method is invoked during tests when it shouldn't be used.
    /// </summary>
    public class DoNotCallException : Exception
    {
        public DoNotCallException(string message) : base(message) { }
    }

    /// <summary>
    /// Interceptor that always throws a NotSupportedException.
    ///
    /// Can be used to ensure methods are not called during tests.
    /// </summary>
    public class DoNotCallAspect : IInterceptor
    {
        public void Intercept(IInvocation invocation)
        {
            throw new DoNotCallException($"{invocation.Method.Name} should not be used.");
        }
    }
}

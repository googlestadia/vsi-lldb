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
using YetiCommon.CastleAspects;

namespace TestsCommon.TestSupport.CastleAspects
{
    /// <summary>
    /// Collection of helper methods to decorate test doubles.
    /// </summary>
    public class TestDoubleProxyHelper
    {
        ProxyGenerator proxyGenerator;

        public TestDoubleProxyHelper(ProxyGenerator proxyGenerator)
        {
            this.proxyGenerator = proxyGenerator;
        }

        /// <summary>
        /// Intercepts a target method to throw an exception.
        ///
        /// This can be used to ensure methods are not used during tests.
        /// </summary>
        /// <typeparam name="T">
        /// The type to decorate. This informs whether to use a class or an interface proxy.
        /// </typeparam>
        /// <param name="target">The object to decorate.</param>
        /// <param name="methodName">The method name to intercept.</param>
        /// <returns>The decorated target.</returns>
        /// <exception cref="ArgumentNullException">If methodName is null.</exception>
        public T DoNotCall<T>(T target, string methodName)
        {
            var hook = new MethodInfoProxyHook(methodName);
            var options = new ProxyGenerationOptions(hook);
            if (typeof(T).IsInterface)
            {
                return (T)proxyGenerator.CreateInterfaceProxyWithTarget(
                    typeof(T), target, options, new DoNotCallAspect());
            }
            return (T)proxyGenerator.CreateClassProxyWithTarget(
                    typeof(T), target, options, new DoNotCallAspect());
        }
    }
}

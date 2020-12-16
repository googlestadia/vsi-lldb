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
    /// Helper utility that encapsulates creation logic for Decorators.
    /// </summary>
    public class DecoratorUtil
    {
        ProxyGenerationOptions returnProxyGenerationOptions;

        public DecoratorUtil() : this(new ProxyGenerationOptions()) { }

        /// <summary>
        ///
        /// </summary>
        /// <param name="returnProxyGenerationOptions">
        /// The proxy generation options to apply when decorating the return values.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if returnProxyGenerationOptions is null.
        /// </exception>
        public DecoratorUtil(ProxyGenerationOptions returnProxyGenerationOptions)
        {
            if (returnProxyGenerationOptions == null)
            {
                throw new ArgumentNullException(nameof(returnProxyGenerationOptions));
            }

            this.returnProxyGenerationOptions = returnProxyGenerationOptions;
        }

        /// <summary>
        /// Creates a decorator for factories. Decorates factories by creating proxies
        /// in place of any of its methods prefixed with "Create". If no aspects are
        /// specified, the decorator returned only checks validity of the
        /// factory being decorated without modifying it.
        /// A valid factory must not have public non-virtual methods, properties
        /// or events, otherwise Decorate will throw an InvalidOperationException.
        /// A factory should implement an interface to be proxied if it requires
        /// public non-virtual members.
        /// </summary>
        /// <param name="aspects">List of aspects to be applied to objects returned from
        /// "Create" methods in decorated factories.</param>
        public IDecorator CreateFactoryDecorator(ProxyGenerator proxyGenerator,
                params IInterceptor[] aspects)
        {
            var proxyHook = new CreateFunctionProxyHook();
            var interceptOnlyCreateFunctions = new ProxyGenerationOptions(proxyHook);
            if (aspects.Length == 0)
            {
                return new ValidateDecorator(proxyGenerator, interceptOnlyCreateFunctions);
            }
            return new Decorator(proxyGenerator, interceptOnlyCreateFunctions,
                new ReturnProxyAspect(
                    new Decorator(proxyGenerator, returnProxyGenerationOptions, aspects)));
        }
    }
}

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
    /// Decorates objects by applying an aspect to it by creating a class or interface proxy
    /// in its place.
    /// </summary>
    public class Decorator : IDecorator
    {
        ProxyGenerator proxyGenerator;
        ProxyGenerationOptions options;
        IInterceptor[] aspects;

        public Decorator(ProxyGenerator proxyGenerator,
            params IInterceptor[] aspects) : this(proxyGenerator,
                new ProxyGenerationOptions(),
                aspects)
        {
        }

        public Decorator(ProxyGenerator proxyGenerator,
            ProxyGenerationOptions options,
            params IInterceptor[] aspects)
        {
            this.proxyGenerator = proxyGenerator;
            this.options = options;
            this.aspects = aspects;
        }

        /// <summary>
        /// Creates a proxy for the given object based on the type specified.
        ///
        /// If the type given is:
        ///   - a class, then a class proxy is used
        ///   - an interface, then an interface proxy is used
        ///
        /// Will assign IDecorator.Self=proxy on |obj| if possible.
        /// </summary>
        /// <param name="type">The proxy type to create.</param>
        /// <param name="obj">The object to decorate.  Can be null.</param>
        /// <returns>A decorator proxy of the requested type, or null if |obj| is null.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown if unable to decorate |obj| for any reason.  Some examples include:
        ///   - type is not a class or interface
        ///   - |type| is not assignable from obj.GetType()
        /// </exception>
        public object Decorate(Type type, object obj)
        {
            if (obj == null)
            {
                return obj;
            }

            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (!type.IsAssignableFrom(obj.GetType()))
            {
                throw new ArgumentException("Decorate should only be used on " +
                    $"classes or interfaces. It was called on type '{type.FullName}'.");
            }

            object proxy = null;
            if (type.IsClass)
            {
                proxy = proxyGenerator.CreateClassProxyWithTarget(type, obj, options, aspects);
            }
            else if (type.IsInterface)
            {
                proxy = proxyGenerator.CreateInterfaceProxyWithTarget(
                    type, obj, options, aspects);
            }
            else
            {
                throw new ArgumentException("Decorate should only be used on " +
                    $"classes or interfaces. It was called on type '{type.FullName}'.");
            }
            DecoratorSelfUtil.TrySetSelf(obj, proxy);
            return proxy;
        }
    }
}
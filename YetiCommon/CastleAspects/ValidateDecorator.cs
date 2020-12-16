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
    /// Implementation of IDecorator that only checks validity of the
    /// factory being decorated without modifying it.
    /// ProxyGenerationOptions may specify what target objects are valid
    /// and Decorate may throw an InvalidOperationException accordingly.
    /// </summary>
    public class ValidateDecorator : IDecorator
    {
        readonly ProxyGenerator proxyGenerator;
        readonly ProxyGenerationOptions options;
        readonly IDecorator decorator;

        public ValidateDecorator(ProxyGenerator proxyGenerator, ProxyGenerationOptions options)
        {
            this.proxyGenerator = proxyGenerator;
            this.options = options;
            decorator = new Decorator(proxyGenerator, options, new NoopAspect());
        }

        public object Decorate(Type type, object obj)
        {
            decorator.Decorate(type, obj);
            return obj;
        }
    }
}

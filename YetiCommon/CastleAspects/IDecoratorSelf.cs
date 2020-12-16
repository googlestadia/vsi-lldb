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

namespace YetiCommon.CastleAspects
{
    /// <summary>
    /// This interface should not be implemented directly. It is required by the
    /// DecoratorSelfProxyHook to enforce proxy targets implement IDecoratorSelf<T>.
    /// </summary>
    public interface IDecoratorSelf { }

    /// <summary>
    /// Defines a pattern to fix leaking "this" when the decorator design pattern is being used.
    ///
    /// Derived classes should use 'Self' instead of 'this' when the reference may be exposed
    /// externally or when trying to access properties/functions with the decorated behavior.
    ///
    /// Example:
    ///   public interface IDomainObject {...}
    ///   public interface DomainObjImpl : SimpleDecoratorSelf<IDomainObject>, IDomainObject {
    ///       ...
    ///       public virtual IDomainObject DoWork() {
    ///           return Self; // Returns the outer most decorated object, preventing a leak of
    ///                        // 'this' and inadvertantly removing the decorated behaviour.
    ///       }
    ///       ...
    ///   }
    ///   public class DomainObjectFactory {
    ///       ...
    ///       public void Create() {
    ///           var concreteDomainObj = new DomainObject();
    ///           var domainObj = new DomainObjectDecorator(concreteDomainObj);
    ///           concreteDomainObj.Self = domainObj;
    ///           return domainObj;
    ///       }
    ///       ...
    ///   }
    /// </summary>
    /// <typeparam name="T">The type that this is known as to clients.  Typically an interface.
    /// </typeparam>
    public interface IDecoratorSelf<T> : IDecoratorSelf
    {
        T Self { get; set; }
    }
}

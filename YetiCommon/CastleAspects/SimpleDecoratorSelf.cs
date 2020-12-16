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

﻿namespace YetiCommon.CastleAspects
{
    /// <summary>
    /// Simple IDecoratorSelf base class.
    ///
    /// Convenience base class that can be used to implement the IDecoratorSelf<T> pattern when the
    /// implementing class implements both T and SimpleDecorator<T>.
    ///
    /// See the IDecoratorSelf class for more documentation.
    /// </summary>
    /// <typeparam name="T">
    /// The type of Self. Must be an interface. Use IDecoratorSelf<T> if a class type is required.
    /// </typeparam>
    public abstract class SimpleDecoratorSelf<T> : IDecoratorSelf<T> where T : class
    {
        /// <exception cref="InvalidCastException">
        /// If the implementing class does not implement T as well.
        /// </exception>
        public SimpleDecoratorSelf()
        {
            // Intermediary cast to object is required because the compiler assumes T isn't an
            // interface and thus T can't be a derivative of SimpleDecoratorSelf<T>.
            Self = (T)(object)this;
        }

        public virtual T Self { get; set; }
    }
}

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
    public static class DecoratorSelfUtil
    {
        /// <summary>
        /// Attempts to set IDecorator<T>.Self on |target| to |proxy|.
        ///
        /// target.Self will be assigned if target is an IDecoratorSelf and target.Self is
        /// assignable to proxy.
        /// </summary>
        /// <param name="target">The target object to assign Self on.</param>
        /// <param name="proxy">The value to assign to target.Self.</param>
        /// <returns>True if successful.</returns>
        /// <exception cref="ArgumentNullException">Thrown if |target|, or |proxy| is null.
        /// </exception>
        public static bool TrySetSelf(object target, object proxy)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            if (proxy == null)
            {
                throw new ArgumentNullException(nameof(proxy));
            }
            if (target is IDecoratorSelf)
            {
                var selfProperty = target.GetType().GetProperty("Self");
                if (selfProperty.PropertyType.IsAssignableFrom(proxy.GetType()))
                {
                    selfProperty.SetValue(target, proxy);
                    return true;
                }
            }
            return false;
        }
    }
}


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
    public interface IDecorator
    {
        /// <summary>
        /// Decorates an object passed in as a reference, and returns a reference
        /// to a decorated object of the same type. If <seealso cref="obj"/> is null,
        /// returns null.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown if unable to decorate |obj| due to invalid arguments.
        /// </exception>
        object Decorate(Type type, object obj);
    }
}

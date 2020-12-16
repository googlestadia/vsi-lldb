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

namespace YetiCommon
{
    public interface ISerializer
    {
        /// <summary>
        /// Serialize the given object.
        /// </summary>
        /// <exception cref="SerializationException">Thrown if the object couldn't be serialized
        /// </exception>
        string Serialize<T>(T obj);

        /// <summary>
        /// Deserialize the given string.
        /// </summary>
        /// <exception cref="SerializationException">Thrown if the string couldn't be deserialized
        /// into the given type.</exception>
        T Deserialize<T>(string text);
    }

    public class SerializationException : Exception
    {
        public SerializationException(string message)
            : base(message) { }

        public SerializationException(Exception inner)
            : base("", inner) { }
    }
}

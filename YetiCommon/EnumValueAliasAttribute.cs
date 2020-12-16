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
    /// <summary>
    /// Indicates that an enum value is an alias for a different value.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class EnumValueAliasAttribute : Attribute
    {
        /// <summary>
        /// Gets the value referred to by this alias.
        /// </summary>
        /// <returns>The real value, or null if no alias is specified</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <see cref="enumType"/> is not a subclass of <see cref="Enum"/>
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when enumValue is null
        /// </exception>
        static public object GetAlias(Type enumType, object enumValue)
        {
            if (!enumType.IsEnum)
            {
                throw new ArgumentException($"{enumType} is not an enum", nameof(enumType));
            }
            if (enumValue == null)
            {
                throw new ArgumentNullException(nameof(enumValue));
            }
            var field = enumType.GetField(Enum.GetName(enumType, enumValue));
            var aliasAttribute =
                GetCustomAttribute(field, typeof(EnumValueAliasAttribute))
                    as EnumValueAliasAttribute;
            return aliasAttribute?.Value;
        }

        /// <summary>
        /// Gets the value referred to by this alias.
        /// </summary>
        /// <typeparam name="T">The type of the enum value</typeparam>
        /// <returns>The real value, or null if no alias is specified</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when T is not a subclass of <see cref="Enum"/>
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when enumValue is null
        /// </exception>
        /// <exception cref="InvalidCastException">
        /// Thrown when the aliased value is not of type T
        /// </exception>
        static public T GetAlias<T>(T enumValue)
        {
            return (T)GetAlias(typeof(T), enumValue);
        }

        /// <summary>
        ///  Gets the value referred to by this alias, or the value itself if it is not an alias.
        /// </summary>
        /// <returns>The real value, or null if no alias is specified</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <see cref="enumType"/> is not a subclass of <see cref="Enum"/>
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when enumValue is null
        /// </exception>
        static public object GetAliasOrValue(Type enumType, object enumValue)
        {
            return GetAlias(enumType, enumValue) ?? enumValue;
        }

        /// <summary>
        ///  Gets the value referred to by this alias, or the value itself if it is not an alias.
        /// </summary>
        /// <returns>The real value, or null if no alias is specified</returns>
        /// <typeparam name="T">The type of the enum value</typeparam>
        /// <exception cref="ArgumentException">
        /// Thrown when <see cref="enumType"/> is not a subclass of <see cref="Enum"/>
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when enumValue is null
        /// </exception>
        /// <exception cref="InvalidCastException">
        /// Thrown when the aliased value is not of type T
        /// </exception>
        static public T GetAliasOrValue<T>(T enumValue)
        {
            return (T)GetAliasOrValue(typeof(T), enumValue);
        }

        /// <summary>
        /// The actual value that should be used.
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// Declare this to be an alias for the given value.
        /// </summary>
        public EnumValueAliasAttribute(object value)
        {
            Value = value;
        }
    }
}

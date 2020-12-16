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

using System;

namespace YetiCommon
{
    public static class EnumUtil
    {
        // Converts |value| to the enum type |T|.
        //
        // For more info see Enum.Parse() reference:
        //   https://msdn.microsoft.com/en-us/library/essfb559(v=vs.110).aspx
        //
        // Example:
        //
        //   enum FavColors { Red, Blue };
        //   enum PrimaryColors { Red, Blue, Green };
        //   var favColor = FavColors.Red;
        //   var primaryColor = favColor.ConvertTo<PrimaryColor>();
        public static T ConvertTo<T>(this object value)
            where T : struct, IConvertible
        {
            var sourceType = value.GetType();
            if (!sourceType.IsEnum)
                throw new ArgumentException("Source type is not enum");
            if (!typeof(T).IsEnum)
                throw new ArgumentException("Destination type is not enum");
            return (T) Enum.Parse(typeof(T), value.ToString(), true);
        }
    }
}

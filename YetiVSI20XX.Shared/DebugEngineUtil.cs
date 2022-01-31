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
using System.Collections.Generic;
using System.Globalization;

namespace YetiVSI
{
    public class DebugEngineUtil
    {
        // Parse a string representing an address in hexadecimal into ulong. Returns false if
        // parsing fails. Assuming the string is in HEX if it starts with "0x", DEC otherwise.
        public static bool GetAddressFromString(string addressStr, out ulong address)
        {
            if (addressStr.StartsWith("0x"))
            {
                addressStr = addressStr.Substring(2);
                return ulong.TryParse(addressStr, NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out address);
            }
            return ulong.TryParse(addressStr, NumberStyles.Number, CultureInfo.InvariantCulture,
                out address);
        }
    }
}

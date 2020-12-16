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

﻿using DebuggerApi;
using System.Text;

namespace YetiVSI.DebugEngine.Variables
{
    public class RemoteValueStringFormat : RemoteValueDefaultFormat, IRemoteValueFormatWithSize
    {
        const uint _maxStringSize = 1024 * 1024;

        readonly Encoding _encoding;
        readonly uint _charSize;
        readonly string _prefix;
        readonly string _postfix;

        public RemoteValueStringFormat(Encoding encoding, uint charSize, string prefix,
                                       string postfix)
        {
            _encoding = encoding;
            _charSize = charSize;  // Everyone likes big chars!
            _prefix = prefix;
            _postfix = postfix;
        }

        public override string FormatValue(RemoteValue remoteValue,
            ValueFormat fallbackValueFormat)
        {
            return FormatValueWithSize(remoteValue, _maxStringSize);
        }

        public string FormatValueWithSize(RemoteValue remoteValue, uint size)
        {
            string result = GetValueAsString(remoteValue, size, out string error);
            if (result == null)
            {
                return error;
            }
            return CStringEscapeHelper.Escape(result, _prefix, _postfix);
        }

        public override string FormatStringView(RemoteValue remoteValue,
            ValueFormat fallbackValueFormat)
        {
            return FormatStringViewWithSize(remoteValue, _maxStringSize);
        }

        public string FormatStringViewWithSize(RemoteValue remoteValue, uint size)
        {
            return GetValueAsString(remoteValue, size, out _);
        }

        public override bool ShouldInheritFormatSpecifier() => true;

        string GetValueAsString(RemoteValue remoteValue, uint size, out string error)
        {
            uint byteSize = size * _charSize;
            byte[] data = remoteValue.GetPointeeAsByteString(_charSize, byteSize, out error);
            if (!string.IsNullOrEmpty(error))
            {
                return null;
            }
            if (data == null)
            {
                error = "<internal error>";
                return null;
            }
            return _encoding.GetString(data);
        }
    }
}

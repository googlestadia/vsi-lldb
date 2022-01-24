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
using System.Linq;
using System.Text;

namespace YetiCommon
{
    // Immutable struct that contains the build ID of a binary or symbol file.
    // Uses the same string representation as LLDB's internal UUID class.
    public struct BuildId : IEquatable<BuildId>
    {
        public static BuildId Empty { get; } = new BuildId();

        public IReadOnlyList<byte> Bytes => bytes ?? Array.Empty<byte>();

        readonly IReadOnlyList<byte> bytes;

        public BuildId(IEnumerable<byte> bytes)
        {
            this.bytes = bytes?.ToArray();
        }

        // Throws FormatException
        public BuildId(string hexStr)
        {
            var byteList = new List<byte>();
            if (hexStr != null)
            {
                var digits = hexStr.Replace("-", null);
                foreach (var c in digits.SkipWhile(Uri.IsHexDigit))
                {
                    throw new FormatException(
                            $"BuildId string '{hexStr}' contains invalid character '{c}'");
                }


                if (digits.Length % 2 != 0)
                {
                    throw new FormatException(
                        $"BuildId string '{hexStr}' does not have an even number of hexadecimal " +
                        "digits");
                }

                for (var i = 0; i < digits.Length; i += 2)
                {
                    byteList.Add(Convert.ToByte(digits.Substring(i, 2), 16));
                }
            }
            bytes = byteList;
        }

        public override string ToString() => ToUUIDString();

        public string ToUUIDString()
        {
            var builder = new StringBuilder();
            for (int i = 0; i < Bytes.Count; ++i)
            {
                if (i == 4 || i == 6 || i == 8 || (i >= 10 && (i - 10) % 6 == 0))
                {
                    builder.Append('-');
                }

                builder.Append(Convert.ToString(Bytes[i], 16).ToUpper().PadLeft(2, '0'));
            }
            return builder.ToString();
        }

        public string ToHexString() =>
            BitConverter.ToString(Bytes.ToArray()).Replace("-", "").ToUpper();

        public override int GetHashCode()
        {
            int hash = 17;
            foreach (var b in Bytes)
            {
                hash = hash * 23 + b;
            }
            return hash;
        }

        public override bool Equals(object other)
        {
            return other is BuildId && this == (BuildId)other;
        }

        public bool Equals(BuildId other)
        {
            return this == other;
        }

        public static bool operator ==(BuildId a, BuildId b)
        {
            return a.Bytes.SequenceEqual(b.Bytes);
        }

        public static bool operator !=(BuildId a, BuildId b)
        {
            return !(a == b);
        }
    }
}

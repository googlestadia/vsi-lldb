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
    /// <summary>
    /// Immutable struct that contains the build ID of a binary or symbol file and its format. Uses
    /// the same string representation as LLDB's internal UUID class.
    /// </summary> 
    public struct BuildId : IEquatable<BuildId>
    {
        // For Windows modules (pdb and pe), amount of bytes that correspond only to the BuildId.
        // The following bytes should correspond to the age.
        const int _windowsBuildIdBytes = 16;
        public static BuildId Empty { get; } = new BuildId();

        public IReadOnlyList<byte> Bytes => bytes ?? Array.Empty<byte>();

        readonly IReadOnlyList<byte> bytes;

        public readonly ModuleFormat ModuleFormat;

        public BuildId(IEnumerable<byte> bytes, ModuleFormat moduleFormat)
        {
            this.bytes = bytes?.ToArray();
            ModuleFormat = moduleFormat;
        }

        /// <summary>
        /// BuildId constructor that parses a string into its bytes representation.
        /// </summary>
        /// <param name="hexStr">Hexadecimal representation of the build ID.</param>
        /// <param name="moduleFormat">Format of the module corresponding to the build ID. Can be
        /// elf, pdb or pe.</param>
        /// <exception cref="FormatException">When the provided string has invalid characters or
        /// doesn't have an even number of digits.</exception>
        public BuildId(string hexStr, ModuleFormat moduleFormat)
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


                if (moduleFormat == ModuleFormat.Elf && digits.Length % 2 != 0)
                {
                    throw new FormatException(
                        $"BuildId string with elf format '{hexStr}' does not have an even" +
                        "number of hexadecimal digits");
                }

                for (var i = 0; i < digits.Length; i += 2)
                {
                    byteList.Add(Convert.ToByte(digits.Substring(i, 2), 16));
                }
            }

            bytes = byteList;
            ModuleFormat = moduleFormat;
        }

        /// <summary>
        /// This method provides a string representation of the BuildId. It differs based on the
        /// module format in the follow way:
        /// - For elf (linux): This is the format used by LLDB. Consists of a hexadecimal
        /// representation spaced by hyphens of the buildId bytes (first 16 bytes) and the age
        /// (following 4 bytes), for instance: 27AC0972-E525-84FE-1A88-B1FE70D1603B-00000102. Note
        /// that even though the age consists of three hex characters, the trailing zeroes are
        /// added.
        /// - For pdb and pe (windows): This representation is used for finding symbols for modules
        /// downloaded from the Microsoft Symbol Store. Consists of a hexadecimal representation of
        /// the buildId bytes (first 16 bytes) and the age (following 4 bytes), for instance:
        /// 27AC0972E52584FE1A88B1FE70D1603B102. Note that in this buildId representation the
        /// trailing zeroes of the age are omitted.
        /// </summary>
        /// <returns></returns>
        public string ToPathName() => ModuleFormat == ModuleFormat.Elf
            ? ToUUIDString()
            : PathNameForWindows();

        string PathNameForWindows()
        {
            var builder = new StringBuilder();
            for (int i = 0; i < Bytes.Count && i < _windowsBuildIdBytes; i++)
            {
                builder.Append(Convert.ToString(Bytes[i], 16).ToUpper().PadLeft(2, '0'));
            }

            bool ageStarted = false;
            for (int i = _windowsBuildIdBytes; i < Bytes.Count; i++)
            {
                if (Bytes[i] > 0 || ageStarted)
                {
                    var hexBytes = Convert.ToString(Bytes[i], 16).ToUpper();
                    if (ageStarted)
                    {
                        hexBytes = hexBytes.PadLeft(2, '0');
                    }
                    builder.Append(hexBytes);
                    ageStarted = true;
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Representation of the UUID string used by LLDB.
        /// </summary>
        /// <returns>String with the buildId bytes in its hexadecimal representation, including
        /// hyphens. Example of the expected format: 27AC0972-E525-84FE-1A88-B1FE70D1603B-00000102.
        /// </returns>
        public string ToUUIDString()
        {
            var builder = new StringBuilder();
            for (int i = 0; i < Bytes.Count; i++)
            {
                if (i == 4 || i == 6 || i == 8 || (i >= 10 && (i - 10) % 6 == 0))
                {
                    builder.Append('-');
                }

                builder.Append(Convert.ToString(Bytes[i], 16).ToUpper().PadLeft(2, '0'));
            }

            return builder.ToString();
        }

        /// <summary>
        /// Provides a string representation of the BuildId, including information of the module
        /// format. This is meant to be used mainly for debugging/logging.
        /// </summary>
        /// <returns>String representation of BuildId including the ModuleFormat.</returns>
        public override string ToString() =>
            $"{{Build-Id: {ToPathName()}, Module format: {ModuleFormat}}}";

        public string ToHexString() =>
            BitConverter.ToString(Bytes.ToArray()).Replace("-", "").ToUpper();

        public override int GetHashCode()
        {
            int hash = 17 + (int)ModuleFormat;
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
            return a.Bytes.SequenceEqual(b.Bytes) && a.ModuleFormat.Equals(b.ModuleFormat);
        }

        public static bool operator !=(BuildId a, BuildId b)
        {
            return !(a == b);
        }
    }
}

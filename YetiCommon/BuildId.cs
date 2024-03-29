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
    public class BuildId
    {
        // For Windows modules (pdb and pe), amount of bytes that correspond only to the BuildId.
        // The following bytes should correspond to the age.
        const int _windowsBuildIdBytes = 16;
        const int _windowsBuildIdAndAgeBytes = 20;

        public readonly IList<byte> Bytes;

        public BuildId(IEnumerable<byte> bytes)
        {
            Bytes = bytes != null
                ? new List<byte>(bytes)
                : new List<byte>();
        }

        /// <summary>
        /// BuildId constructor that parses a string into its bytes representation.
        /// </summary>
        /// <param name="hexStr">Hexadecimal representation of the build ID.</param>
        /// <exception cref="FormatException">When the provided string has invalid characters or
        /// doesn't have an even number of digits.</exception>
        public BuildId(string hexStr)
        {
            var byteList = new List<byte>();
            if (hexStr != null)
            {
                string digits = hexStr.Replace("-", null);
                foreach (char c in digits.SkipWhile(Uri.IsHexDigit))
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

                for (int i = 0; i < digits.Length; i += 2)
                {
                    byteList.Add(Convert.ToByte(digits.Substring(i, 2), 16));
                }
            }

            Bytes = byteList;
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
        public string ToPathName(ModuleFormat moduleFormat) => moduleFormat == ModuleFormat.Elf
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
            $"{{Build-Id: {ToUUIDString()}}}";

        public string ToHexString() =>
            BitConverter.ToString(Bytes.ToArray()).Replace("-", "").ToUpper();

        public static bool IsNullOrEmpty(BuildId buildId) => (buildId?.Bytes.Count ?? 0) == 0;

        /// <summary>
        /// Compares the current buildId against other buildId to validate if they match. The
        /// validation takes into account the format of the modules. For pdb module format, the age
        /// is disregarded.
        /// </summary>
        /// <param name="otherBuildId">Build Id against the comparison is made.</param>
        /// <param name="moduleFormat">Module format being used.</param>
        /// <returns>True if the Bytes match, excluding the age for buildId with pdb module format.
        /// </returns>
        public bool Matches(BuildId otherBuildId, ModuleFormat moduleFormat)
        {
            if (otherBuildId == null)
            {
                return false;
            }

            if (Bytes.Count != otherBuildId.Bytes.Count)
            {
                return false;
            }

            int byteLengthToValidate;
            switch (moduleFormat)
            {
                case ModuleFormat.Pe:
                    byteLengthToValidate = _windowsBuildIdAndAgeBytes;
                    break;
                case ModuleFormat.Pdb:
                    byteLengthToValidate = _windowsBuildIdBytes;
                    break;
                case ModuleFormat.Elf:
                default:
                    byteLengthToValidate = Bytes.Count;
                    break;
            }

            for (int i = 0; i < byteLengthToValidate && i < Bytes.Count; i++)
            {
                if (!Bytes[i].Equals(otherBuildId.Bytes[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
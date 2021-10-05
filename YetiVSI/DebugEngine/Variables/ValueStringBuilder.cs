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
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YetiVSI.DebugEngine.NatvisEngine;

namespace YetiVSI.DebugEngine.Variables
{
    public static class ValueStringBuilder
    {
        const int _maxValueStringLength = 80;

        static readonly string _cvPattern = "(const|volatile|const volatile|volatile const)";

        // Matches (possibly CV-qualified) void pointer types.
        static readonly Regex _voidPointerRegex =
            new Regex($"^({_cvPattern} )?void ?\\* ?{_cvPattern}?$");

        // TODO: Consider enabling preview for Natvis variables.
        public static async Task<string> BuildAsync(IVariableInformation varInfo,
                                                    int charactersLeft = _maxValueStringLength)
        {
            if (charactersLeft <= 0)
            {
                return "...";
            }

            string value = await UnwrapPointerValueAsync(varInfo, charactersLeft);
            if (!string.IsNullOrEmpty(value) || !varInfo.MightHaveChildren())
            {
                return value;
            }

            return await FormatChildrenListAsync(varInfo, charactersLeft - 2);
        }

        static async Task<string> FormatChildrenListAsync(IVariableInformation varInfo,
                                                          int charactersLeft,
                                                          string noChildrenMarker = "")
        {
            IChildAdapter children = varInfo.GetChildAdapter();
            if (children is INatvisEntity)
            {
                return "";
            }

            int childrenCount = await children.CountChildrenAsync();

            const int maxChildren = 3;
            StringBuilder sb = new StringBuilder();
            sb.Append("{");
            for (int i = 0; i < childrenCount; i++)
            {
                try
                {
                    IVariableInformation currentChild =
                        (await children.GetChildrenAsync(i, 1)).ElementAt(0).GetCachedView();

                    // For array elements or pointers, we do not display the child name.
                    // Also array elements are separated by commas. We detect the array elements
                    // using a somewhat hacky way - we check if their name is the index in
                    // square brackets.
                    bool isArrayElementOrPointee =
                        currentChild.DisplayName == $"[{i}]" || currentChild.DisplayName == "";

                    if (i != 0)
                    {
                        sb.Append(isArrayElementOrPointee ? ", " : " ");
                    }

                    // If we are over the limit, let us just display the dots and exit.
                    if (!isArrayElementOrPointee && i >= maxChildren)
                    {
                        sb.Append("...");
                        break;
                    }

                    string childValue =
                        await BuildAsync(currentChild, charactersLeft - sb.Length - 1);

                    if (string.IsNullOrEmpty(childValue) && currentChild.GetChildAdapter()
                                                                is INatvisEntity)
                    {
                        childValue = "{...}";
                    }
                    else if (childValue == "..." || string.IsNullOrEmpty(childValue))
                    {
                        sb.Append(childValue);
                        break;
                    }

                    if (!isArrayElementOrPointee)
                    {
                        sb.Append($"{currentChild.DisplayName}=");
                    }
                    sb.Append(childValue);
                }
                catch (ArgumentOutOfRangeException)
                {
                    break;
                }
            }
            if (childrenCount == 0)
            {
                sb.Append(noChildrenMarker);
            }
            sb.Append("}");
            return sb.ToString();
        }

        /// <summary>
        /// Checks whether we are dealing with a pointer or not.
        /// For pointers, it assignes values as Native Visual Studio would do.
        /// If we have a pointer to a struct, it calls ExtractChildren(),
        /// since the childValue is an empty string.
        /// </summary>
        static async Task<string> UnwrapPointerValueAsync(IVariableInformation varInfo,
                                                          int charactersLeft)
        {
            string value = await varInfo.ValueAsync();
            if (!varInfo.IsPointer)
            {
                return value;
            }

            // For pointers, the assignment value is equal to the memory address and the
            // value is the content stored at that location.
            string plainValue = varInfo.AssignmentValue;
            string memoryAddress = varInfo.GetMemoryAddressAsHex();
            string addressPrefix =
                FormatSpecifierUtil.SuppressMemoryAddress(varInfo.FormatSpecifier) ||
                string.IsNullOrEmpty(memoryAddress)
                    ? ""
                    : memoryAddress + " ";
            if (value != "" && plainValue != value)
            {
                return addressPrefix + value;
            }

            // Void pointers can't be unwrapped.
            if (_voidPointerRegex.IsMatch(varInfo.TypeName))
            {
                return memoryAddress;
            }

            if (varInfo.IsNullPointer())
            {
                return addressPrefix + "<NULL>";
            }

            return addressPrefix +
                   await FormatChildrenListAsync(
                       varInfo, charactersLeft - $"{addressPrefix}{{}}".Length, "???");
        }
    }
}

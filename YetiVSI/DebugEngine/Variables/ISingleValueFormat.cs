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

using DebuggerApi;
using System.Collections.Generic;

namespace YetiVSI.DebugEngine.Variables
{
    /// <summary>
    /// Implementations of this interface are responsible for applying formatting
    /// rules to various properties of single RemoteValues.
    /// </summary>
    public interface ISingleValueFormat
    {
        /// <summary>
        /// Gets the ValueFormat which should be used to format values. If there is no format
        /// specifier defined, |fallbackValueFormat| is used.
        /// </summary>
        ValueFormat GetValueFormat(ValueFormat fallbackValueFormat);

        /// <summary>
        /// For format specifiers that affect the display value of a RemoteValue, this will return
        /// an implementation specific display value, otherwise this will return
        /// remoteValue.GetDisplayValue().
        /// </summary>
        string FormatValue(RemoteValue remoteValue, ValueFormat fallbackValueFormat);

        /// <summary>
        /// For format specifiers that affect the string view of a RemoteValue, this will return
        /// an implementation specific string view, otherwise this will return an unescaped version
        /// of the result of FormatValue().
        /// </summary>
        string FormatStringView(RemoteValue remoteValue, ValueFormat fallbackValueFormat);

        /// <summary>
        /// For format specifiers that require special formatting of assigned expressions, this
        /// will try to apply the proper format to the provided expression. If it does not succeed,
        /// it returns |expression| as is.
        /// </summary>
        string FormatExpressionForAssignment(RemoteValue remoteValue, string expression);

        /// <summary>
        /// Gets the value which should be used for assignment operations. Defaults to
        /// |remoteValue.GetValue(.)|.
        /// </summary>
        string GetValueForAssignment(RemoteValue remoteValue, ValueFormat fallbackValueFormat);

        /// <summary>
        /// Returns true if the format specifier should be inherited to child values.
        /// </summary>
        bool ShouldInheritFormatSpecifier();

        /// <summary>
        /// Formats the given remote value as an address. The remote value must be a pointer
        /// or a reference (if it is not, the method will return an empty string).
        /// </summary>
        string FormatValueAsAddress(RemoteValue remoteValue);
    }
}

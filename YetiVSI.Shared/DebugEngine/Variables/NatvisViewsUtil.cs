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

﻿namespace YetiVSI.DebugEngine.Variables
{
    class NatvisViewsUtil
    {
        const string _viewStart = "view(";
        const string _viewEnd = ")";

        /// <summary>
        /// Returns "foo" if |formatSpecifier| is of the form "view(foo)" and an empty string
        /// otherwise.
        /// </summary>
        public static string ParseViewFormatSpecifier(string formatSpecifier)
        {
            formatSpecifier = FormatSpecifierUtil.RemoveRawFormatSpecifierPrefix(formatSpecifier);
            if (!formatSpecifier.StartsWith(_viewStart) || !formatSpecifier.EndsWith(_viewEnd))
            {
                return string.Empty;
            }

            return formatSpecifier.Substring(_viewStart.Length,
                                             formatSpecifier.Length - _viewStart.Length -
                                             _viewEnd.Length);
        }

        /// <summary>
        /// Figures out whether some item should be visible based on the variable's
        /// |formatSpecifier| (e.g. view(theView)) and the item's |includeView| and |excludeView|
        /// attributes.
        /// </summary>
        /// <returns>Returns false iff |includeView| is defined and does not match theView or if
        /// |excludeView| is defined and matches theView. Here, theView is understood to be empty
        /// if not defined.</returns>
        public static bool IsViewVisible(string formatSpecifier, string includeView,
                                         string excludeView)
        {
            string view = ParseViewFormatSpecifier(formatSpecifier);

            includeView = string.IsNullOrEmpty(includeView) ? string.Empty : includeView.Trim();
            if (includeView.Length != 0 && view != includeView)
            {
                return false;
            }

            excludeView = string.IsNullOrEmpty(excludeView) ? string.Empty : excludeView.Trim();
            if (excludeView.Length != 0 && view == excludeView)
            {
                return false;
            }

            return true;
        }
    }
}
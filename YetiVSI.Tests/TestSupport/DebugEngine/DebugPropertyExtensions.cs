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

using Microsoft.VisualStudio.Debugger.Interop;

namespace YetiVSI.Test.TestSupport.DebugEngine
{
    public static class DebugPropertyExtensions
    {
        /// <summary>
        /// Helper function to retrieve the DEBUG_PROPERTY_INFO.
        /// </summary>
        /// <param name="fields"></param>
        /// <param name="propertyInfo"></param>
        /// <returns></returns>
        public static int GetPropertyInfo(this IDebugProperty2 debugProperty,
            enum_DEBUGPROP_INFO_FLAGS fields,
            out DEBUG_PROPERTY_INFO propertyInfo)
        {
            var propertyInfos = new DEBUG_PROPERTY_INFO[1];
            var result = debugProperty.GetPropertyInfo(fields, 10, 0, null, 0, propertyInfos);
            propertyInfo = propertyInfos[0];
            return result;
        }
    }
}

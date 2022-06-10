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

﻿using System.Collections.Generic;
using System.Threading.Tasks;

namespace YetiVSI.DebugEngine.Variables
{
    public interface IChildAdapter
    {
        /// <summary>
        /// Count the number of children for the current element.
        /// </summary>
        /// <returns>The number of children.</returns>
        Task<int> CountChildrenAsync();

        /// <summary>
        /// Get children in range [from; from + count). If childrenCount is less than from + count,
        /// it returns children in range [from, childrenCount).
        /// </summary>
        /// <param name="from">Index of the first requested children (non-negative).</param>
        /// <param name="count">Number of children (non-negative).</param>
        /// <returns>Children in the requested range.</returns>
        Task<IList<IVariableInformation>> GetChildrenAsync(int from, int count);
    }
}

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

﻿using Google.VisualStudioFake.Internal.UI;
using Microsoft.VisualStudio.Debugger.Interop;
using System;

namespace Google.VisualStudioFake.Internal
{
    /// <summary>
    /// Encapsulates data handling methods for variable entries.
    /// </summary>
    public interface IVariableDataSource
    {
        /// <summary>
        /// Gets an DEBUG_PROPERTY_INFO containing the current state of the variable.
        /// </summary>
        /// <param name="varEntry">
        /// Value whose state is requested.
        /// </param>
        /// <param name="callback">
        /// Callback called once the data retrieval is done.
        /// </param>
        void GetCurrentState(IVariableEntryInternal varEntry,
                             Action<DEBUG_PROPERTY_INFO> callback);
    }
}

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

namespace Google.VisualStudioFake.API.UI
{
    public interface IWatchWindow
    {
        /// <summary>
        /// Adds an expression to be watched when the program stops.
        /// </summary>
        IVariableEntry AddWatch(string expression);

        /// <summary>
        /// Gets the expressions currently managed by this view.
        /// </summary>
        IList<IVariableEntry> GetWatchEntries();

        /// <summary>
        /// Deletes all the expressions currently managed by this view.
        /// </summary>
        IList<IVariableEntry> DeleteAllWatchEntries();
    }
}

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

﻿using DebuggerApi;
using System.Collections.Generic;

namespace YetiVSI.DebugEngine.Variables
{
    /// <summary>
    /// Implementations of this interface are responsible for applying formatting
    /// rules to various properties of RemoteValues and providing information about
    /// children (based on the format restrictions).
    /// </summary>
    public interface IRemoteValueFormat : ISingleValueFormat
    {
        /// <summary>
        /// For format specifiers that affect the size of an array expansion, this will return
        /// a value that is implementation specific, otherwise this will return
        /// remoteValue.GetNumChildren().
        /// </summary>
        uint GetNumChildren(RemoteValue remoteValue);

        /// <summary>
        /// Returns children with indices in the range [offset, offset + count). Unless the format
        /// specifier affects the size of an array expansion, this will pull from all the
        /// remoteValue's children.
        /// </summary>
        IEnumerable<RemoteValue> GetChildren(RemoteValue remoteValue, int offset, int count);
    }
}

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
using System.Text;

namespace YetiVSI.DebugEngine.Variables
{
    public interface IRemoteValueFormatWithSize
    {
        /// <summary>
        /// Formats the remote value with the specified size.
        /// </summary>
        string FormatValueWithSize(RemoteValue remoteValue, uint size);

        /// <summary>
        /// Formats the remote value for string view with the specified size.
        /// </summary>
        string FormatStringViewWithSize(RemoteValue remoteValue, uint size);
    }
}

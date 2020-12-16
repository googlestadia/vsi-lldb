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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YetiVSI.DebugEngine.Variables
{
    /// <summary>
    /// Provides the number of children requested by a scalar array format specifier.
    /// </summary>
    public class ScalarNumChildrenProvider : IRemoteValueNumChildrenProvider
    {
        readonly uint size;

        public ScalarNumChildrenProvider(uint size)
        {
            this.size = size;
        }

        public string Specifier => size.ToString();

        public uint GetNumChildren(RemoteValue remoteValue) => size;
    }
}

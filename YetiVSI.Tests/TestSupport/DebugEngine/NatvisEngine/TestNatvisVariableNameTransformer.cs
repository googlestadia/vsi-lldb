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
using YetiVSI.DebugEngine.NatvisEngine;

namespace YetiVSI.Test.TestSupport.DebugEngine.NatvisEngine
{
    /// <summary>
    /// An IVariableNameTransformer is used to create unique names with the same source name for
    /// use in a particular scope. The test version of a variable name transformer just returns the
    /// argument passed to TransformName. A limitation is that if a CustomListItems expansion is
    /// done twice during a test there will be a name collision. Because our tests don't currently
    /// talk to a real LLDB debugger this isn't going to surface as a problem.
    /// </summary>
    class TestNatvisVariableNameTransformer : IVariableNameTransformer
    {
        public string TransformName(string name) => name;
    }
}

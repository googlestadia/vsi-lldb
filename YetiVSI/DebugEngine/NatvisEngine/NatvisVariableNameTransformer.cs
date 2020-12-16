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

﻿namespace YetiVSI.DebugEngine.NatvisEngine
{
    public interface IVariableNameTransformer
    {
        string TransformName(string name);
    }

    /// <summary>
    /// We currently don't have the benefit of controlling the scope of scratch variables that we
    /// create in our natvis implementation. This class is used to transform a given name to a new
    /// one for use in a particular scope. It is expected that calling TransformName with the same
    /// argument multiple times will yield a different result. It is up to the caller to manage the
    /// mapping between the original name and the result returned by TransformName.
    /// </summary>
    public class NatvisVariableNameTransformer : IVariableNameTransformer
    {
        uint _counter;

        public string TransformName(string name) => $"${name}_{NextInt}";

        uint NextInt => _counter++;
    }
}

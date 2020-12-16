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

using System.Collections.Generic;

namespace LldbApi
{
    // Interface mirrors the SBFunction API as closely as possible.
    public interface SbFunction
    {
        /// <summary>
        /// Get the start address of the function.
        /// </summary>
        SbAddress GetStartAddress();

        /// <summary>
        /// Get the end address of the function.
        /// </summary>
        SbAddress GetEndAddress();

        /// <summary>
        /// Returns the node's name.
        /// </summary>
        LanguageType GetLanguage();

        /// <summary>
        /// Returns a list of disassembled instructions.
        /// </summary>
        List<SbInstruction> GetInstructions(SbTarget target);

        /// <summary>
        /// Returns the name of the function.
        /// </summary>
        string GetName();

        /// <summary>
        /// Returns the function type.
        /// </summary>
        SbType GetType();

        /// <summary>
        /// Returns the function's argument name at |index|.
        /// </summary>
        string GetArgumentName(uint index);
    }
}

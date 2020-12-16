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
    // Interface mirrors the SBInstruction API as closely as possible.
    public interface SbInstruction
    {
        // Returns the address of the instruction.
        SbAddress GetAddress();

        // Returns the operands of the instruction.
        string GetOperands(SbTarget target);

        // Returns the Mnemonic of the instruction.
        string GetMnemonic(SbTarget target);

        // Returns the comment of the instruction.
        string GetComment(SbTarget target);

        // Returns the byte size of the instruction.
        ulong GetByteSize();
    }
}

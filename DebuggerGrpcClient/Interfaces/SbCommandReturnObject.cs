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

using System;

namespace DebuggerApi
{
    // The SbCommandReturnObjhect wraps up the results from a command execution performed via the
    // SbCommandInterpreter.
    //
    // Interface mirrors SBCommandReturnObject API as closely as posible.
    public interface SbCommandReturnObject
    {
        // Returns true if the command was successful.
        bool Succeeded();

        // Returns output messages.
        string GetOutput();

        // Returns error messages.
        string GetError();

        // Returns summary description including output/error messages and status.
        string GetDescription();
    }
}

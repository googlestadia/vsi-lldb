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

using DebuggerCommonApi;

namespace DebuggerApi
{
    // Interface mirrors the SBAddress API as closely as possible.
    public interface SbAddress
    {
        // A unique identifier for the address.  Not part of the LLDB API.
        long GetId();

        // Get the line entry.
        LineEntryInfo GetLineEntry();

        // Get the load address.
        ulong GetLoadAddress(RemoteTarget target);

        // Get information about the function this address is associated with.
        SbFunction GetFunction();

        // Get information about the symbol this address is associated with.
        SbSymbol GetSymbol();
    }
}

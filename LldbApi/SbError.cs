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

namespace LldbApi
{
    // Interface mirrors the SBError API as closely as possible.
    public interface SbError
    {
        // True when this error represents a failure.
        bool Fail();

        // True when the operation completed successfully.
        bool Success();

        // Get an error code describing the error.
        uint GetError();

        // Get a string describing the error.
        string GetCString();
    }
}

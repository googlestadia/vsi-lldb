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

namespace LldbApi
{
    // Interface mirrors the SBLineEntry API as closely as possible.
    public interface SbLineEntry
    {
        // Get the file name that this line entry belongs too.
        string GetFileName();

        // Returns the directory of the source file,
        string GetDirectory();

        // Get the line number of this line entry.
        uint GetLine();

        // Get the column number of this line entry.
        uint GetColumn();

        // Get the address for the beginning of the line entry.
        SbAddress GetStartAddress();

        // Get the file spec for the line entry.
        SbFileSpec GetFileSpec();
    }
}

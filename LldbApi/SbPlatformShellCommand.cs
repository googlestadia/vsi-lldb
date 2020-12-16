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
    // Interface mirrors the SBPlatformShellCommand API as closely as possible.
    public interface SbPlatformShellCommand
    {
        // Get the command to execute.
        string GetCommand();

        // Get the output of the command.
        string GetOutput();

        // Get the exit code of the command.
        int GetStatus();

        // Get the signal which caused the command to exit.
        int GetSignal();

        // Sets the resulting command output.  Not part of the LLDB API.
        void SetOutput(string output);

        // Sets the resulting command status.  Not part of the LLDB API.
        void SetStatus(int status);

        // Sets the resulting command signal.  Not part of the LLDB API.
        void SetSignal(int signal);
    }
}

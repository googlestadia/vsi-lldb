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

namespace YetiVSI
{
    public static class PkgCmdID
    {
        // Command definitions must match YetiVSI.vsct.
        public const int cmdidCrashDumpAttachCommand = 256;
        public const int cmdidLLDBShellExec = 257;
        public const int cmdidDebuggerOptionsCommand = 258;
        public const int cmdidReportBug = 259;
    };
}

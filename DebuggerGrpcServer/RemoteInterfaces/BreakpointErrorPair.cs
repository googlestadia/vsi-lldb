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

﻿namespace DebuggerGrpcServer.RemoteInterfaces
{
    public struct BreakpointErrorPair
    {
        public readonly RemoteBreakpoint breakpoint;
        public readonly BreakpointError error;

        public BreakpointErrorPair(RemoteBreakpoint breakpoint, BreakpointError error)
        {
            this.breakpoint = breakpoint;
            this.error = error;
        }
    }
}

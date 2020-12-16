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

﻿using LldbApi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DebuggerGrpcServer
{
    public class RemoteBreakpointFactory
    {
        public RemoteBreakpoint Create(SbBreakpoint sbBreakpoint) =>
            sbBreakpoint != null ? new RemoteBreakpointImpl(sbBreakpoint, this) : null;
    }

    class RemoteBreakpointImpl : RemoteBreakpoint
    {
        private readonly SbBreakpoint sbBreakpoint;
        private readonly RemoteBreakpointFactory breakpointFactory;

        internal RemoteBreakpointImpl(SbBreakpoint sbBreakpoint,
            RemoteBreakpointFactory breakpointFactory)
        {
            this.sbBreakpoint = sbBreakpoint;
            this.breakpointFactory = breakpointFactory;
        }

        #region RemoteBreakpoint

        public void SetEnabled(bool enabled) => sbBreakpoint.SetEnabled(enabled);

        public uint GetNumLocations() => sbBreakpoint.GetNumLocations();

        public SbBreakpointLocation GetLocationAtIndex(uint index) =>
            sbBreakpoint.GetLocationAtIndex(index);

        public SbBreakpointLocation FindLocationById(int id) => sbBreakpoint.FindLocationById(id);

        public uint GetHitCount() => sbBreakpoint.GetHitCount();

        public int GetId() => sbBreakpoint.GetId();

        public void SetIgnoreCount(uint ignoreCount) => sbBreakpoint.SetIgnoreCount(ignoreCount);

        public void SetOneShot(bool isOneShot) => sbBreakpoint.SetOneShot(isOneShot);

        public void SetCondition(string condition) => sbBreakpoint.SetCondition(condition);

        public void SetCommandLineCommands(IEnumerable<string> commands) =>
            sbBreakpoint.SetCommandLineCommands(commands.ToList());

        public SbBreakpoint GetSbBreakpoint() => sbBreakpoint;

        #endregion
    }
}

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

﻿using Google.VisualStudioFake.API;
using Microsoft.VisualStudio.Debugger.Interop;
using System;
using System.Diagnostics;
using System.Linq;

namespace Google.VisualStudioFake.Internal.Interop
{
    public class EventCallbackFake : IDebugEventCallback2
    {
        readonly IJobOrchestrator jobOrchestrator;

        public EventCallbackFake(IJobOrchestrator jobOrchestrator)
        {
            this.jobOrchestrator = jobOrchestrator;
        }

        public int Event(IDebugEngine2 pEngine, IDebugProcess2 pProcess, IDebugProgram2 pProgram,
            IDebugThread2 pThread, IDebugEvent2 pEvent, ref Guid riidEvent, uint dwAttrib)
        {
            Trace.WriteLine($"VSFake: Received IDebugEvent2 {{type:\"{pEvent.GetType()}\", " +
                $"interfaces:{GetInteropInterfacesTrace(pEvent)}}}");
            return jobOrchestrator.HandleCallbackEvent(pEngine, pProcess, pProgram, pThread,
                pEvent);
        }

        static string GetInteropInterfacesTrace(object obj)
        {
            var interfaces = obj.GetType().GetInterfaces()
                .Where(i => i.Name != nameof(
                    IDebugEvent2) && i.Namespace == typeof(IDebugEvent2).Namespace)
                .Select(i => $"\"{i.Name}\"");
            return string.Join(", ", interfaces);
        }
    }
}

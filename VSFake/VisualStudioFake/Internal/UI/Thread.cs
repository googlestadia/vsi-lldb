// Copyright 2021 Google LLC
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
using Google.VisualStudioFake.API;
using Google.VisualStudioFake.API.UI;
using Microsoft.VisualStudio.Debugger.Interop;

namespace Google.VisualStudioFake.Internal.UI
{
    public class Thread : IThread
    {
        readonly IDebugThread2 _thread;
        readonly THREADPROPERTIES _props;
        readonly IDebugSessionContext _debugSessionContext;

        public Thread(IDebugThread2 thread, THREADPROPERTIES props,
                      IDebugSessionContext debugSessionContext)
        {
            _thread = thread;
            _props = props;
            _debugSessionContext = debugSessionContext;
        }

        #region IThread

        public uint ThreadId => _props.dwThreadId;
        public uint SuspendCount => _props.dwSuspendCount;
        public uint ThreadState => _props.dwThreadState;
        public string Priority => _props.bstrPriority;
        public string Name => _props.bstrName;
        public string Location => _props.bstrLocation;
        public void Select() => _debugSessionContext.SelectedThread = _thread;
        public bool IsSelected => _debugSessionContext.SelectedThread == _thread;

        public override string ToString() => string.IsNullOrEmpty(Name)
            ? "<invalid>"
            : Name;

        #endregion
    }
}
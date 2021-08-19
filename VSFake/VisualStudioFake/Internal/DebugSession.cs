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

using Google.VisualStudioFake.API;
using Google.VisualStudioFake.API.UI;

namespace Google.VisualStudioFake.Internal
{
    /// <summary>
    /// This class manages models and views for the debug session.
    /// </summary>
    public class DebugSession : IDebugSession
    {
        public DebugSession(IDebugSessionContext context, IBreakpointView breakpointView,
                            IControlFlowView controlFlowView, IThreadsWindow threadsWindow,
                            ICallStackWindow callStackWindow, IWatchWindow watchWindow)
        {
            Context = context;
            BreakpointView = breakpointView;
            ControlFlowView = controlFlowView;
            ThreadsWindow = threadsWindow;
            CallStackWindow = callStackWindow;
            WatchWindow = watchWindow;
        }

        public IDebugSessionContext Context { get; }

        #region UI elements

        public IBreakpointView BreakpointView { get; }
        public IControlFlowView ControlFlowView { get; }
        public IThreadsWindow ThreadsWindow { get; }
        public ICallStackWindow CallStackWindow { get; }
        public IWatchWindow WatchWindow { get; }

        #endregion
    }
}
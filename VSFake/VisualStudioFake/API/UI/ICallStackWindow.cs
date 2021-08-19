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

using System.Collections.Generic;

namespace Google.VisualStudioFake.API.UI
{
    public enum CallStackWindowState
    {
        /// <summary>
        /// Frames are stale and have to be refreshed.
        /// </summary>
        NotRefreshed,

        /// <summary>
        /// Frames are ready to be retrieved.
        /// </summary>
        Ready,

        /// <summary>
        /// Frames are queued to be refresh. Must wait until ready.
        /// </summary>
        Pending,
    }

    public interface ICallStackWindow
    {
        /// <summary>
        /// Gets all stack frames in the currently selected thread.
        /// Must be in Ready state.
        /// </summary>
        /// <returns>List of available stack frames</returns>
        List<IStackFrame> GetStackFrames();

        /// <summary>
        /// Gets the current state of the call stack window.
        /// </summary>
        CallStackWindowState State { get; }

        /// <summary>
        /// Shortcut for State == CallStackWindowState.Ready.
        /// </summary>
        bool Ready { get; }
    }
}
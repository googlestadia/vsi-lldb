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

namespace Google.VisualStudioFake.API.UI
{
    public interface IThread
    {
        uint ThreadId { get; }
        uint SuspendCount { get; }
        uint ThreadState { get; }
        string Priority { get; }
        string Name { get; }
        string Location { get; }

        /// <summary>
        /// Sets this thread as selected thread, so that the call stack window
        /// shows call stacks of this thread.
        /// </summary>
        void Select();

        /// <summary>
        /// Returns true if this thread is the currently selected thread.
        /// </summary>
        bool IsSelected { get; }
    }
}

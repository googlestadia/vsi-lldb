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

namespace LldbApi
{
    /// <summary>
    ///  Interface mirrors the SBBroadcaster API as closely as possible.
    /// </summary>
    public interface SbBroadcaster
    {
        /// <summary>
        /// Add listener to broadcaster.
        /// </summary>
        uint AddListener(SbListener listener, uint eventMask);

        /// <summary>
        /// Remove listener from broadcaster.
        /// </summary>
        uint RemoveListener(SbListener listener, uint eventMask);
    }
}

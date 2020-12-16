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

﻿namespace YetiVSI.DebugEngine.Variables
{
    /// <summary>
    /// This interface is aimed to mark entities for which it might take O(N) to get children count,
    /// where N is a number of children. Without a limit on count it can have performance
    /// implications. Thus, such elements must implement this interface and only count children up
    /// to the children limit.
    /// </summary>
    public interface IHasChildrenLimit
    {
        /// <summary>
        /// The parameter to control the limit of children. It can be configured from outside and
        /// the implementation is supposed to update its size if the ChildrenLimit is increased.
        /// </summary>
        int ChildrenLimit { get; set; }
    }
}
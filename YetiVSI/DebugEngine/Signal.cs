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

using System.Collections.Generic;

namespace YetiCommon
{
    // Consciously made this a struct as Signals are meant to represent a discrete Signal with
    // immutable data. Passing by reference is undesirable in most cases.
    public struct Signal
    {
        public string name;
        public List<string> alias;
        public string description;
        public int code;
        public bool stop;
    }
}

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

namespace LldbApi
{
    // Enumeration of process state reflected in events.
    public enum StateType
    {
        INVALID = 0,
        CONNECTED = 2,
        STOPPED = 5,
        RUNNING = 6,
        DETACHED = 9,
        EXITED = 10,
    };

    public enum EventType
    {
        STATE_CHANGED = (1 << 0),
        INTERRUPT = (1 << 1),
        STRUCTURED_DATA = (1 << 5),
    };

    // Interface mirrors the SBEvent API as closely as possible.
    public interface SbEvent
    {
        // Returns the type of the event.
        EventType GetEventType();

        // Returns the description for the event.
        string GetDescription();

        // Returns the process state type if the event contained a state change.  Not part of the
        // LLDB API.
        StateType GetStateType();

        // Returns true if the process has resumed running after stop.
        bool GetProcessRestarted();
    }
}

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

﻿using System;

namespace Google.VisualStudioFake.API
{
    [Flags]
    public enum ExecutionSyncPoint
    {
        ENGINE_CREATED = (1 << 0),
        PROCESS_CREATED = (1 << 1),
        PROGRAM_SELECTED = (1 << 2),
        THREAD_SELECTED = (1 << 3),
        FRAME_SELECTED = (1 << 4),
        BREAK = (1 << 5),
        IDLE = (1 << 6),
        PROGRAM_RUNNING = (1 << 7),
        PROGRAM_TERMINATED = (1 << 8),
    }
}

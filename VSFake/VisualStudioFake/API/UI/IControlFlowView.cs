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

﻿using Google.VisualStudioFake.Internal.ExecutionSyncPoint;

namespace Google.VisualStudioFake.API.UI
{
    public interface IControlFlowView
    {
        /// <summary>
        /// Instructs the attached program to continue.
        ///
        /// Returns when the attached job queue is empty or the program terminates.
        /// </summary>
        [SyncPoint(ExecutionSyncPoint.IDLE | ExecutionSyncPoint.PROGRAM_TERMINATED,
            Timeout = VSFakeTimeout.Medium)]
        void Continue();

        /// <summary>
        /// Instructs the attached program to pause.
        ///
        /// Returns when the attached program breaks.
        /// </summary>
        [SyncPoint(ExecutionSyncPoint.BREAK)]
        void Pause();

        /// <summary>
        /// Instructs the attached program to stop execution.
        ///
        /// Returns when the attached program terminates.
        /// </summary>
        [SyncPoint(ExecutionSyncPoint.PROGRAM_TERMINATED)]
        void Stop();

        /// <summary>
        /// Instructs the attached program to step into instructions.
        ///
        /// Returns when the attached program breaks.
        /// </summary>
        [SyncPoint(ExecutionSyncPoint.BREAK)]
        void StepInto();

        /// <summary>
        /// Instructs the attached program to step over current instruction.
        ///
        /// Returns when the attached program breaks.
        /// </summary>
        [SyncPoint(ExecutionSyncPoint.BREAK)]
        void StepOver();

        /// <summary>
        /// Instructs the attached program to step out of current method.
        ///
        /// Returns when the attached program breaks.
        /// </summary>
        [SyncPoint(ExecutionSyncPoint.BREAK)]
        void StepOut();
    }
}

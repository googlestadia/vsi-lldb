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
using System;

namespace Google.VisualStudioFake.API
{
    /// <summary>
    /// Defines the different job queue execution modes for an ISessionDebugManager.
    /// </summary>
    public enum SDMExecutionMode
    {
        /// <summary>
        /// VSFake API's will automatically process the job queue according to the
        /// SyncPointAttribute contract defined on the method.
        /// </summary>
        AUTO,
        /// <summary>
        /// Job queue will ignore the SyncPointAttribute contracts defined on the VSFake API's.
        /// </summary>
        MANUAL
    }

    public interface ISDMExecutionScope : IDisposable { }

    public interface ISessionDebugManager
    {
        SDMExecutionMode ExecutionMode { get; }

        /// <summary>
        /// Transitions to a manual execution mode.
        /// </summary>
        /// <remarks>
        /// Example: using(var _ = sdm.StartManualMode()) { ... }
        /// </remarks>
        /// <returns>An execution scope that will revert back to AUTO mode once disposed.</returns>
        ISDMExecutionScope StartManualMode();

        /// <summary>
        /// Runs the job executor until all jobs are executed. If the timeout ends before then, an
        /// exception will be thrown.
        /// </summary>
        /// <param name="timeout">countdown timer after which an exception will be thrown</param>
        void RunUntilIdle(TimeSpan timeout);

        /// <summary>
        /// Runs the job executor until a break event happens. If the timeout ends before
        /// the method returns, an exception will be thrown.
        /// </summary>
        /// <param name="timeout">countdown timer after which an exception will be thrown</param>
        void RunUntilBreak(TimeSpan timeout);

        /// <summary>
        /// Runs the job executor until a given sync point.
        /// </summary>
        /// <param name="syncPoint">The sync point to return on.</param>
        /// <param name="timeout">countdown timer after which an exception will be thrown</param>
        void RunUntil(ExecutionSyncPoint syncPoint, TimeSpan timeout);

        /// <summary>
        /// Runs the job executor until a given predicate becomes true.
        /// </summary>
        /// <param name="predicate">The predicate that is polled between jobs.</param>
        /// <param name="timeout">countdown timer after which an exception will be thrown</param>
        void RunUntil(Func<bool> predicate, TimeSpan timeout);

        /// <summary>
        /// Start a launch and attach flow.
        ///
        /// Returns when IDebugSessionContext.DebugProgram has been updated.
        /// </summary>
        [SyncPoint(ExecutionSyncPoint.PROGRAM_SELECTED, Timeout = VSFakeTimeout.LaunchAndAttach)]
        void LaunchAndAttach();
    }
}

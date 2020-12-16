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

﻿using Google.VisualStudioFake.API.UI;
using System;
using System.Collections.Generic;

namespace Google.VisualStudioFake.API
{
    public static class IVSFakeExtensions
    {
        #region ISessionDebugManager

        /// <summary>
        /// Runs the job executor until all jobs are executed. If the timeout ends before then, an
        /// exception will be thrown.
        /// </summary>
        /// <remarks>
        /// Convenience function that calls ISessionDebugManager.RunUntilIdle() with the
        /// RunUntilIdle timeout defined in IVSFake.Timeouts.
        /// </remarks>
        public static void RunUntilIdle(this IVSFake vsFake) =>
            vsFake.SessionDebugManager.RunUntilIdle(vsFake.Timeouts[VSFakeTimeout.RunUntilIdle]);

        /// <summary>
        /// Runs the job executor until a break event happens. If the timeout ends before
        /// the method returns, an exception will be thrown.
        /// </summary>
        /// <remarks>
        /// Convenience function that calls ISessionDebugManager.RunUntilBreak() with the
        /// RunUntilBreak timeout defined in IVSFake.Timeouts.
        /// </remarks>
        public static void RunUntilBreak(this IVSFake vsFake) =>
            vsFake.SessionDebugManager.RunUntilBreak(vsFake.Timeouts[VSFakeTimeout.RunUntilBreak]);

        /// <summary>
        /// Runs the job executor until a given sync point.
        /// </summary>
        /// <remarks>
        /// Convenience function that calls ISessionDebugManager.RunUntil() with the
        /// RunUntil timeout defined in IVSFake.Timeouts.
        /// </remarks>
        public static void RunUntil(this IVSFake vsFake, ExecutionSyncPoint syncPoint) =>
            vsFake.SessionDebugManager.RunUntil(syncPoint, vsFake.Timeouts[VSFakeTimeout.RunUntil]);

        /// <summary>
        /// Runs the job executor until a given predicate becomes true.
        /// </summary>
        /// <remarks>
        /// Convenience function that calls ISessionDebugManager.RunUntil() with the
        /// RunUntil timeout defined in IVSFake.Timeouts.
        /// </remarks>
        public static void RunUntil(this IVSFake vsFake, Func<bool> predicate) =>
            vsFake.SessionDebugManager.RunUntil(predicate, vsFake.Timeouts[VSFakeTimeout.RunUntil]);

        /// <summary>
        /// Runs the job executor until all variables in the Watch window are ready.
        /// </summary>
        /// <remarks>
        /// Convenience function that calls ISessionDebugManager.RunUntil() with the
        /// Predicates.AllVarsReady predicate and RunUntil timeout defined in IVSFake.Timeouts.
        /// </remarks>
        public static void RunUntilAllVarsReady(this IVSFake vsFake) =>
            vsFake.SessionDebugManager.RunUntil(
                Predicates.AllVarsReady(vsFake.DebugSession.WatchWindow.GetWatchEntries()),
                vsFake.Timeouts[VSFakeTimeout.RunUntil]);

        /// <summary
        /// Start a launch and attach flow.
        ///
        /// Returns when IDebugSessionContext.DebugProgram has been updated.
        /// </summary>
        /// <remarks>
        /// Convenience function that calls ISessionDebugManager.LaunchAndAttach().
        /// </remarks>
        public static void LaunchAndAttach(this IVSFake vsFake) =>
            vsFake.SessionDebugManager.LaunchAndAttach();

        #endregion

        #region IControlFlowView

        /// <summary>
        /// Instructs the attached program to continue.
        /// </summary>
        /// <remarks>
        /// Convenience function that calls IControlFlowView.Continue().
        /// </remarks>
        public static void Continue(this IVSFake vsFake) =>
            vsFake.DebugSession.ControlFlowView.Continue();

        /// <summary>
        /// Instructs the attached program to pause.
        /// </summary>
        /// <remarks>
        /// Convenience function that calls IControlFlowView.Pause().
        /// </remarks>
        public static void Pause(this IVSFake vsFake) =>
            vsFake.DebugSession.ControlFlowView.Pause();

        /// <summary>
        /// Instructs the attached program to stop execution.
        /// </summary>
        /// <remarks>
        /// Convenience function that calls IControlFlowView.Stop().
        /// </remarks>
        public static void Stop(this IVSFake vsFake) =>
            vsFake.DebugSession.ControlFlowView.Stop();

        /// <summary>
        /// Instructs the attached program to stop execution if it has started.
        /// </summary>
        /// <remarks>
        /// Convenience function that calls IControlFlowView.Stop() when the program state is
        /// either Running or AtBreak.
        /// </remarks>
        public static void StopIfStarted(this IVSFake vsFake)
        {
            if (vsFake.DebugSession.Context.ProgramState == ProgramState.Running ||
                vsFake.DebugSession.Context.ProgramState == ProgramState.AtBreak)
            {
                vsFake.DebugSession.ControlFlowView.Stop();
            }
        }

        /// <summary>
        /// Instructs the attached program to step into instructions.
        /// </summary>
        /// <remarks>
        /// Convenience function that calls IControlFlowView.SteInto().
        /// </remarks>
        public static void StepInto(this IVSFake vsFake) =>
            vsFake.DebugSession.ControlFlowView.StepInto();

        /// <summary>
        /// Instructs the attached program to step over current instruction.
        /// </summary>
        /// <remarks>
        /// Convenience function that calls IControlFlowView.StepOver().
        /// </remarks>
        public static void StepOver(this IVSFake vsFake) =>
            vsFake.DebugSession.ControlFlowView.StepOver();

        /// <summary>
        /// Instructs the attached program to step out of current method.
        /// </summary>
        /// <remarks>
        /// Convenience function that calls IControlFlowView.StepOut().
        /// </remarks>
        public static void StepOut(this IVSFake vsFake) =>
            vsFake.DebugSession.ControlFlowView.StepOut();

        #endregion

        #region IBreakpointView

        /// <summary>
        /// Adds a new breakpoint specified by a filename and a line number.
        /// </summary>
        /// <remarks>
        /// Convenience function that calls IBreakpointView.Add().
        /// </remarks>
        public static IBreakpoint AddBreakpoint(
            this IVSFake vsFake, string filename, int lineNumber) =>
            vsFake.DebugSession.BreakpointView.Add(filename, lineNumber);

        /// <summary>
        /// Gets the breakpoints currently managed by the breakpoint view.
        /// </summary>
        /// <remarks>
        /// Convenience function that calls IBreakpointView.GetBreakpoints().
        /// </remarks>
        public static IList<IBreakpoint> GetBreakpoints(this IVSFake vsFake) =>
            vsFake.DebugSession.BreakpointView.GetBreakpoints();

        /// <summary>
        /// Deletes all the breakpoints currently managed by the breakpoint view.
        /// </summary>
        /// <remarks>
        /// Convenience function that calls IBreakpointView.DeleteAllBreakpoints().
        /// </remarks>
        public static IList<IBreakpoint> DeleteAllBreakpoints(this IVSFake vsFake) =>
            vsFake.DebugSession.BreakpointView.DeleteAllBreakpoints();

        #endregion

        #region IWatchWindow

        /// <summary>
        /// Adds an expression to be watched when the program execution pauses.
        /// </summary>
        /// <remarks>
        /// Convenience function that calls IWatchWindow.AddWatch().
        /// </remarks>
        public static IVariableEntry AddWatch(this IVSFake vsFake, string expression) =>
            vsFake.DebugSession.WatchWindow.AddWatch(expression);

        /// <summary>
        /// Gets the expressions currently managed by the watch window.
        /// </summary>
        /// <remarks>
        /// Convenience function that calls IWatchWindow.GetWatchEntries().
        /// </remarks>
        public static IList<IVariableEntry> GetWatchEntries(this IVSFake vsFake) =>
            vsFake.DebugSession.WatchWindow.GetWatchEntries();

        /// <summary>
        /// Deletes all the entries in the Watch window.
        /// </summary>
        /// <remarks>
        /// Convenience function that calls IWatchWindow.DeleteAllWatchEntries().
        /// </remarks>
        public static IList<IVariableEntry> DeleteAllWatchEntries(this IVSFake vsFake) =>
            vsFake.DebugSession.WatchWindow.DeleteAllWatchEntries();

        #endregion

        #region IDebugSession

        /// <summary>
        /// Enables or disables hexadecimal display of integer variables.
        /// </summary>
        /// <remarks>
        /// Convenience function that sets vsFake.DebugSession.Context.HexadecimalDisplay.
        /// </remarks>
        public static void SetHexadecimalDisplay(this IVSFake vsFake, bool enabled)
        {
            vsFake.DebugSession.Context.HexadecimalDisplay = enabled;
        }

        #endregion
    }
}

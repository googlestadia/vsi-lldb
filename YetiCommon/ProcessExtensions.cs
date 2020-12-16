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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace YetiCommon
{
    /// <summary>
    /// Indicates that a process exited with a non-zero exit code.
    /// </summary>
    public class ProcessExecutionException : ProcessException
    {
        /// <summary>
        ///  The non-zero exit code which caused the process to exit.
        /// </summary>
        public int ExitCode { get; private set; }

        /// <summary>
        /// Optional process output captured during its execution.
        /// </summary>
        public List<string> OutputLines { get; private set; } = new List<string>();

        /// <summary>
        /// Optional process error output captured during its execution.
        /// </summary>
        public List<string> ErrorLines { get; private set; } = new List<string>();

        public ProcessExecutionException(string message, int exitCode) : base(message)
        {
            ExitCode = exitCode;
        }

        public ProcessExecutionException(string message, int exitCode,
            List<string> outputLines, List<string> errorLines)
            : base(message)
        {
            ExitCode = exitCode;
            OutputLines = outputLines;
            ErrorLines = errorLines;
        }
    }

    public static class ProcessExtensions
    {
        /// <summary>
        /// Attempts to start the process and returns a task that is completed when the process
        /// exits with an exit code of zero (success).
        /// </summary>
        /// <exception cref="ProcessException">
        /// Thrown if the process cannot be started, or if it does not exit within the timeout
        /// period that was specified when the process was created.
        /// </exception>
        /// <exception cref="ProcessExecutionException">
        /// Thrown if the process exits with a non-zero exit code.
        /// </exception>
        /// <remarks>
        /// Output and error data handlers are guaranteed to be called before this task completes.
        /// </remarks>
        public static async Task RunToExitWithSuccessAsync(this IProcess process)
        {
            int code = await process.RunToExitAsync();
            process.CheckExitCode(code);
        }

        /// <summary>
        /// Checks exit code of the process. If it is non-zero it throws
        /// the ProcessExecutionException.
        /// </summary>
        /// <exception cref="ProcessExecutionException">
        /// Thrown if the process exits with a non-zero exit code.
        /// </exception>
        /// <param name="code">Exit code of the process.</param>
        public static void CheckExitCode(this IProcess process, int code)
        {
            if (code != 0)
            {
                throw new ProcessExecutionException(
                    ErrorStrings.ProcessExitedWithErrorCode(process.ProcessName, code), code);
            }
        }

        /// <summary>
        /// Attempts to start the process and returns a task that is completed when the process
        /// exits with an exit code of zero (success). Standard output and error output are
        /// captured.
        /// </summary>
        /// <returns>A task evaluates to the output of the process.</returns>
        /// <exception cref="ProcessException">
        /// Thrown if the process cannot be started, or if it does not exit within the timeout
        /// period that was specified when the process was created.
        /// </exception>
        /// <exception cref="ProcessExecutionException">
        /// Thrown if the process exits with a non-zero exit code. Use the OutputLines and
        /// ErrorLines properties to get the process output and error text, respectively.
        /// </exception>
        public static async Task<List<string>> RunToExitWithSuccessCapturingOutputAsync(
            this IProcess process)
        {
            List<string> outputLines = new List<string>();
            List<string> errorLines = new List<string>();
            process.OutputDataReceived += (obj, args) =>
            {
                if (args.Text != null)
                {
                    outputLines.Add(args.Text);
                }
            };
            process.ErrorDataReceived += (obj, args) =>
            {
                if (args.Text != null)
                {
                    errorLines.Add(args.Text);
                }
            };

            var code = await process.RunToExitAsync();
            if (code != 0)
            {
                throw new ProcessExecutionException(
                    ErrorStrings.ProcessExitedWithErrorCode(process.ProcessName, code),
                    code, outputLines, errorLines);
            }
            return outputLines;
        }
    }
}

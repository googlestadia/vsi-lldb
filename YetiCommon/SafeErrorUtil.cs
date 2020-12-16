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
using System.Diagnostics;
using System.Threading.Tasks;

namespace YetiCommon
{
    /// <summary>
    /// Methods for working with errors in contexts where we must not propagate exceptions:
    ///   - catch and finally blocks
    ///   - finalizers
    /// </summary>
    public static class SafeErrorUtil
    {
        /// <summary>
        /// Handle any errors produced by the given async function. The resulting task never fails.
        /// </summary>
        /// <param name="func">The function to execute asynchronously</param>
        /// <param name="handler">The action to perform on any error</param>
        /// <remarks>
        /// It is undefined what thread/executor runs the handler.
        /// Exceptions thrown by the handler are silently ignored.
        /// </remarks>
        public static async Task SafelyHandleErrorAsync(Func<Task> func, Action<Exception> handler)
        {
            try
            {
                await func();
            }
            catch (Exception e)
            {
                try
                {
                    handler(e);
                }
                catch
                {
                    // Ignore all errors from handler.
                }
            }
        }

        /// <summary>
        /// Handle any exceptions thrown by the given action and never throw ourselves.
        /// </summary>
        /// <param name="func">The action to execute synchronously</param>
        /// <param name="handler">The action to perform on any error</param>
        public static void SafelyHandleError(Action action, Action<Exception> handler)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                try
                {
                    handler(e);
                }
                catch
                {
                    // Ignore all errors from handler.
                }
            }
        }

        /// <summary>
        /// Logs any errors produced by the given async function, and ignores its result.
        /// </summary>
        /// <param name="func">The function to execute asynchronously</param>
        /// <param name="message">Message that is prepended to the error</param>
        public static void SafelyLogErrorAndForget(Func<Task> func, string message) =>
            SafelyLogErrorAsync(func, message);

        /// <summary>
        /// Logs any errors produced by the given async function. The resulting task never fails.
        /// </summary>
        /// <param name="func">The function to execute asynchronously</param>
        /// <param name="message">Message that is prepended to the error</param>
        public static Task SafelyLogErrorAsync(Func<Task> func, string message) =>
            SafelyHandleErrorAsync(func, ErrorLoggerWithMessage(message));

        /// <summary>
        /// Log any exceptions thrown by the given action and never throw ourselves.
        /// </summary>
        /// <param name="func">The action to execute synchronously</param>
        /// <param name="message">Message that is prepended to the error</param>
        public static void SafelyLogError(Action action, string message) =>
            SafelyHandleError(action, ErrorLoggerWithMessage(message));

        static Action<Exception> ErrorLoggerWithMessage(string message)
        {
            return e => Trace.WriteLine($"{message}: " + e.ToString());
        }
    }
}

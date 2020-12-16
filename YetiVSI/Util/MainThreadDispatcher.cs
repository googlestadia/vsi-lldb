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
using System.Windows;

namespace YetiVSI.Util
{
    /// <summary>
    /// Interface representing a class that can dispatch work items to be executed on a specific
    /// thread.
    /// </summary>
    public interface IDispatcher
    {
        /// <summary>
        /// Asynchonously dispatches the given action, without blocking or waiting for the action to
        /// complete. If multiple actions are dispatched, they are guaranteed to be invoked in the
        /// order that they are dispatched.
        /// </summary>
        void Post(Action action);
    }

    /// <summary>
    /// Helper class for dispatching work items to the main UI thread.
    /// </summary>
    public class MainThreadDispatcher : IDispatcher
    {
        public void Post(Action action)
        {
            // Using Dispatcher.InvokeAsync shouldn't deadlock as long as we don't wait on the
            // result. As such, disable the warning that normally appears when using InvokeAsync.
#pragma warning disable VSTHRD001
            Application.Current.Dispatcher.InvokeAsync(action);
#pragma warning restore VSTHRD001
        }
    }
}

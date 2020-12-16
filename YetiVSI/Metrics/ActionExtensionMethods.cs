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

﻿using GgpGrpc.Cloud;
using GgpGrpc.Cloud.Interceptors;
using System;

namespace YetiVSI.Metrics
{
    /// <summary>
    /// Extension methods related to <see cref="Action"/> and <see cref="ActionRecorder"/>.
    /// </summary>
    public static class ActionExtensionMethods
    {
        // Run the task and record its outcome plus duration.
        // See ActionRecorder.RecordCancelable for details.
        public static bool RunAndRecord(this ICancelableTask task,
            ActionRecorder recorder, ActionType type)
        {
            return recorder.CreateToolAction(type).Record(() => task.Run());
        }

        // Run the task and record its outcome plus duration.
        // See ActionRecorder.RecordCancelable for details.
        public static bool RunAndRecord(this ICancelableTask task, IAction action)
        {
            return action.Record(() => task.Run());
        }

        /// <summary>
        /// Intercept all cloud calls using the given action.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public static ICloudRunner Intercept(this ICloudRunner runner, IAction action)
        {
            if (runner == null) { throw new ArgumentNullException(nameof(runner)); }
            if (action == null) { throw new ArgumentNullException(nameof(action)); }

            return new CloudRunnerProxy(runner, new MetricsInterceptor(action));
        }
    }
}

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

﻿using System.Reflection;

namespace YetiCommon.MethodRecorder
{
    /// <summary>
    /// Interface to record information about method invocations.
    /// </summary>
    public interface IMethodInvocationRecorder
    {
        /// <summary>
        /// Records invocation about a method invocation. Thread-safe.
        /// </summary>
        /// <param name="methodInfo">Information about the callee method.</param>
        /// <param name="startTimestampUs">Timestamp just before the method is called, in
        /// microseconds. This timestamp is relative to the start of the debug session.</param>
        /// <param name="endTimestampUs">Timestamp immediately after the method ends its
        /// execution, in microseconds. This timestamp is relative to the start of the debug
        /// session.</param>
        void Record(MethodInfo methodInfo, long startTimestampUs, long endTimestampUs);
    }
}

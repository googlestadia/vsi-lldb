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
using System.Reflection;

namespace YetiCommon.ExceptionRecorder
{
    /// <summary>
    /// Interface to record information about exceptions.
    /// </summary>
    public interface IExceptionRecorder
    {
        /// <summary>
        /// Record an exception at the given callsite.
        /// </summary>
        /// <param name="callSite">Callsite that caught/handled the exception</param>
        /// <param name="ex">The exception to be recorded</param>
        /// <exception cref="ArgumentNullException">Thrown if any argument is null</exception>
        /// <remarks>
        /// Each exception should on be recorded at most once.
        /// </remarks>
        void Record(MethodBase callSite, Exception ex);
    }
}

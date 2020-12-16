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

namespace TestsCommon.TestSupport
{
    /// <summary>
    /// Thrown from test doubles that were partially implemented but do not support the current
    /// feature.
    /// </summary>
    ///
    /// <remarks>
    /// Used to differentiate from the common NotImplementedException that could also be thrown
    /// by production code. This exception should be used in situations where a test double
    /// (e.g. fake) is implemented on an is-needed basis and does not support the specific
    /// invocation.
    ///
    /// If this exception causes a test to fail it is an indication that
    ///   1) the system under test requires a more feature rich test double or
    ///   2) the system under test is using the test double in an unexpected way
    ///
    /// </remarks>
    public class NotImplementedTestDoubleException : Exception
    {
        private static string DEFAULT_MSG = "The test double does not support that feature.";

        public NotImplementedTestDoubleException() : base(DEFAULT_MSG) { }

        public NotImplementedTestDoubleException(string msg) : base(msg) { }
    }
}

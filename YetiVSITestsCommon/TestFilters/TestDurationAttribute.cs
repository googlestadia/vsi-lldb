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

﻿using NUnit.Framework;
using System;

namespace YetiVSITestsCommon.TestFilters
{
    /// <summary>
    /// Approximate expected duration of a test.
    /// </summary>
    public enum TestDuration
    {
        FewSeconds,    // 1 sec - 10 sec
        ManySeconds,   // 10 sec - 1 min
        MinuteOrMore,  // 1 min or more
    }

    /// <summary>
    /// Used to filter test cases.
    /// </summary>
    /// <remarks>
    /// Example usage:
    /// - "Trait:TestDuration_MinuteOrMore" in Visual Studio's Test Explorer to select slow tests.
    /// - "-Trait:TestDuration_ManySeconds -Trait:TestDuration_MinuteOrMore" excludes slow tests.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class TestDurationAttribute : PropertyAttribute
    {
        public TestDurationAttribute(TestDuration testDuration)
            : base("TestDuration", "TestDuration_" + testDuration)
        {
            TestDuration = testDuration;
        }

        public TestDuration TestDuration { get; }
    }
}

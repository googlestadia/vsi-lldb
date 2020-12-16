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

namespace YetiVSI.Test.Metrics.TestSupport
{
    class TestClass
    {
        public static readonly MethodInfo MethodInfo1 = typeof(TestClass).GetMethod("TestMethod1");
        public static readonly MethodInfo MethodInfo2 = typeof(TestClass).GetMethod("TestMethod2");
        public static readonly MethodInfo MethodInfo3 = typeof(TestClass).GetMethod("TestMethod3");

        public void TestMethod1() { }
        public void TestMethod2() { }
        public void TestMethod3() { }
    }
}

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
using YetiVSI.Metrics;

namespace YetiVSI.Test.Metrics
{
    [TestFixture]
    public class MethodInfoExtensionsTests
    {
        [Test]
        public void TestGetProto()
        {
            var testMethodInfo = typeof(MethodInfoExtensionTestClass).GetMethods()[0];
            var proto = testMethodInfo.GetProto();

            Assert.AreEqual("YetiVSI.Test.Metrics", proto.NamespaceName);
            Assert.AreEqual("MethodInfoExtensionTestClass", proto.ClassName);
            Assert.AreEqual("TestMethod", proto.MethodName);
        }

        private class MethodInfoExtensionTestClass
        {
            public void TestMethod() { }
        }
    }
}

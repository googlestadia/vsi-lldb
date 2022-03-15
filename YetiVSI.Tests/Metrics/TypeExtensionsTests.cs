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

using NUnit.Framework;
using YetiVSI.Metrics;

namespace YetiVSI.Test.Metrics
{
    [TestFixture]
    public class TypeExtensionsTests
    {
        [Test]
        public void TestGetProto()
        {
            var testType = typeof(TypeExtensionTestClass);
            var proto = testType.GetProto();

            Assert.AreEqual("YetiVSI.Test.Metrics", proto.NamespaceName);
            Assert.AreEqual("TypeExtensionTestClass", proto.ClassName);
        }

        private class TypeExtensionTestClass { }
    }
}

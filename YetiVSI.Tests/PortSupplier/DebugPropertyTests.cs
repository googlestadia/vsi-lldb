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

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using NUnit.Framework;
using YetiVSI.PortSupplier;

namespace YetiVSI.Test.PortSupplier
{
    [TestFixture]
    class DebugPropertyTests
    {
        [Test]
        public void GetPropertyInfo()
        {
            var testName = "test name";
            var testValue = "test value";

            var property = new DebugProperty(testName, testValue);
            var propertyInfo = new DEBUG_PROPERTY_INFO[1];
            Assert.AreEqual(VSConstants.S_OK, property.GetPropertyInfo(
                (enum_DEBUGPROP_INFO_FLAGS)0, 0, 0, new IDebugReference2[0], 0, propertyInfo));
            Assert.AreEqual(testName, propertyInfo[0].bstrName);
            Assert.AreEqual(testValue, propertyInfo[0].bstrValue);
            Assert.AreEqual(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME |
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE, propertyInfo[0].dwFields);
        }
    }
}

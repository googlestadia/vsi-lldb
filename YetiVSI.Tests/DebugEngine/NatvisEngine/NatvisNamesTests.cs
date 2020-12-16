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
using System.Collections;
using System.Linq;
using YetiVSI.DebugEngine.NatvisEngine;

namespace YetiVSI.Test.DebugEngine.NatvisEngine
{
    [TestFixture]
    public class NatvisNamesTests
    {
        [TestCaseSource(typeof(ParseSimpleTypeData), "TestCases")]
        public string ParseSimpleType(string typeName)
        {
            return TypeName.Parse(typeName).FullyQualifiedName;
        }

        [Test]
        public void ParseEmptyType()
        {
            Assert.IsNull(TypeName.Parse(string.Empty));
        }
    }

    public class ParseSimpleTypeData
    {
        public static IEnumerable TestCases => new string[]
        {
            "signed char", "unsigned char", "char16_t", "char32_t", "wchar_t", "char",
            "signed short int", "signed short", "unsigned short int", "unsigned short",
            "short int", "short", "signed int", "unsigned int", "int", "signed long int",
            "unsigned long int", "long int", "long", "signed long long int", "long long int",
            "unsigned long long int", "long long", "float", "double", "long double", "bool",
            "void", "unsigned long", "uint32_t", "int8_t",
            "unsigned char __attribute__((ext_vector_type(32)))", "long float", "unsigned",
            "unsigned long long", "signed"
        }.Select(typeName => new TestCaseData(typeName).Returns(typeName));
    }
}

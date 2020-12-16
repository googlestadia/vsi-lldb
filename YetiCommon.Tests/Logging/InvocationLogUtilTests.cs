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

﻿using Castle.DynamicProxy;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Reflection;
using System.Text;
using YetiCommon.Logging;
using YetiCommon.Tests.CastleAspects.TestSupport;
using TestsCommon.TestSupport;
using static TestsCommon.TestSupport.StringExtensions;

namespace YetiCommon.Tests.Logging
{
    class DummyObjectWithGenerics<T>
    {
        public void DoSomething<T1, T2>(T1 param1, T2 param2) { }
    }

    public class InvocationLogUtilTest_Hepler
    {
        public static ParameterInfo[] CreateParameters(params string[] paramNames)
        {
            var paramInfo = new ParameterInfo[paramNames.Length];
            for (int i = 0; i < paramNames.Length; ++i)
            {
                var param = Substitute.For<ParameterInfo>();
                param.Name.Returns(paramNames[i]);
                paramInfo[i] = param;
            }
            return paramInfo;
        }
    }

    [TestFixture]
    class InvocationLogUtilTests
    {
        InvocationLogUtil logUtil;
        StringBuilder stringBuilder;

        [SetUp]
        public void SetUp()
        {
            logUtil = new InvocationLogUtil();
            stringBuilder = new StringBuilder();
        }

        [Test]
        public void AppendCallInformation()
        {
            var invocationStub = Substitute.For<IInvocation>();

            var methodInfo =
                MethodInfoUtil.GetMethodInfo<DummyObjectWithGenerics<int>>(
                    obj => obj.DoSomething(1, "value"));
            var args = new object[]
                {
                    1,
                    "value"
                };

            logUtil.AppendCallInformation(stringBuilder, methodInfo, args);

            var actual = stringBuilder.ToString();
            Assert.That(actual, Does.Contain(
                "YetiCommon.Tests.Logging.DummyObjectWithGenerics`1[System.Int32]"));
            Assert.That(actual, Does.Contain(
                "Void DoSomething[Int32,String](Int32, System.String)"));
            Assert.That(actual, Does.Contain("Args [ToString]:"));
            Assert.That(actual, Does.Contain("param1=1"));
            Assert.That(actual, Does.Contain("param2=value"));
            Assert.That(actual, Does.Contain("Args [Json]:"));
            Assert.That(actual, Does.Contain(JoinLines(
                @"(",
                @"{",
                @"  ""param1"": 1,",
                @"  ""param2"": ""value""",
                @"}",
                @")",
                @"")));
        }
    }

    [TestFixture]
    public class InvocationLogUtil_AppendArgumentsToStringSerializationTests
    {
        class ErrorObj
        {
            public override string ToString()
            {
                throw new Exception("Dummy Exception Message");
            }
        }

        InvocationLogUtil logUtil;
        StringBuilder stringBuilder;

        [SetUp]
        public void SetUp()
        {
            logUtil = new InvocationLogUtil();
            stringBuilder = new StringBuilder();
        }

        [Test]
        public void ErrorObjectSerialization()
        {
            var args = new object[]
            {
                new ErrorObj()
            };

            var param1 = Substitute.For<ParameterInfo>();
            param1.Name.Returns("param1");
            var paramInfo = InvocationLogUtilTest_Hepler.CreateParameters(
                "param1");

            logUtil.AppendArgumentsToString(stringBuilder, paramInfo, args);

            Assert.That(stringBuilder.ToString(), Does.Contain("param1=<Unknown>"));
            Assert.That(stringBuilder.ToString(), Does.Contain("Dummy Exception Message"));
        }

        [Test]
        public void MultipleArgSerialization()
        {
            var args = new object[]
            {
                100,
                "value2",
                null
            };

            var paramInfo = InvocationLogUtilTest_Hepler.CreateParameters(
                "param1", "param2", "param3");

            logUtil.AppendArgumentsToString(stringBuilder, paramInfo, args);

            var expectedLog = JoinLines(
                @"(",
                @"  param1=100,",
                @"  param2=value2,",
                @"  param3=null",
                @")",
                @"");
            Assert.That(stringBuilder.ToString(), Is.EqualTo(expectedLog));
        }
    }

    [TestFixture]
    public class InvocationLogUtil_AppendArgumentsJsonSerializationTests
    {
        public class SimpleObj
        {
            public int Number = 9;
        }

        public class ArrayObj
        {
            public int[] Numbers = new int[] { 1, 2, 3 };
        }

        public class ComplexObj
        {
            public SimpleObj SimpleObj = new SimpleObj();
            public ArrayObj ArrayObj = new ArrayObj();
#pragma warning disable 414 // disable unused field warning
            private int PrivateVar = 42;
#pragma warning restore 414
        }

        public class ErrorObj
        {
            public int Number = 7;

            public int ExceptionThrowingProperty
            {
                get
                {
                    throw new Exception("Dummy Exception Message");
                }
            }
        }

        InvocationLogUtil logUtil;
        StringBuilder stringBuilder;

        [SetUp]
        public void SetUp()
        {
            logUtil = new InvocationLogUtil();
            stringBuilder = new StringBuilder();
        }

        [Test]
        public void AppendArgumentsForEmptyArguments()
        {
            object[] args = new object[0];
            var paramInfo = InvocationLogUtilTest_Hepler.CreateParameters();

            logUtil.AppendArgumentsJson(stringBuilder, paramInfo, args);

            Assert.That(stringBuilder.ToString(), Is.EqualTo("()" + Environment.NewLine));
        }

        [Test]
        public void AppendArgumentsForOneArg()
        {
            object[] args = new object[]
            {
                1
            };
            var paramInfo = InvocationLogUtilTest_Hepler.CreateParameters("param1");

            logUtil.AppendArgumentsJson(stringBuilder, paramInfo, args);
            var expected = JoinLines(
                @"(",
                @"{",
                @"  ""param1"": 1",
                @"}",
                @")",
                @"");
            Assert.That(stringBuilder.ToString(), Is.EqualTo(expected));
        }

        [Test]
        public void AppendArgumentsForSomeArg()
        {
            object[] args = new object[]
            {
                1,
                "value",
                null
            };
            var paramInfo = InvocationLogUtilTest_Hepler.CreateParameters(
                "param1", "param2", "param3");

            logUtil.AppendArgumentsJson(stringBuilder, paramInfo, args);

            Assert.That(stringBuilder.ToString(), Is.EqualTo(JoinLines(
                @"(",
                @"{",
                @"  ""param1"": 1,",
                @"  ""param2"": ""value"",",
                @"  ""param3"": null",
                @"}",
                @")",
                @"")));
        }

        [Test]
        public void ErrorObjectSerialization()
        {
            var args = new object[]
            {
                new ErrorObj(),
                9
            };
            var paramInfo = InvocationLogUtilTest_Hepler.CreateParameters("param1", "param2");

            logUtil.AppendArgumentsJson(stringBuilder, paramInfo, args);

            Assert.That(stringBuilder.ToString(), Does.Contain(@"""Number"": 7"));
            Assert.That(stringBuilder.ToString(), Does.Contain("ExceptionThrowingProperty"));
            Assert.That(stringBuilder.ToString(), Does.Contain("Dummy Exception Message"));
            Assert.That(stringBuilder.ToString(), Does.Contain("\"param2\": 9"));
        }

        [Test]
        public void ComplexObjectSerialization()
        {
            var args = new object[]
            {
                new ArrayObj(),
                new ComplexObj()
            };
            var paramInfo = InvocationLogUtilTest_Hepler.CreateParameters("param1", "param2");

            logUtil.AppendArgumentsJson(stringBuilder, paramInfo, args);

            var expected = JoinLines(
                @"(",
                @"{",
                @"  ""param1"": {",
                @"    ""Numbers"": [",
                @"      1,",
                @"      2,",
                @"      3",
                @"    ]",
                @"  },",
                @"  ""param2"": {",
                @"    ""SimpleObj"": {",
                @"      ""Number"": 9",
                @"    },",
                @"    ""ArrayObj"": {",
                @"      ""Numbers"": [",
                @"        1,",
                @"        2,",
                @"        3",
                @"      ]",
                @"    }",
                @"  }",
                @"}",
                @")",
                @"");
            Assert.That(stringBuilder.ToString(), Is.EqualTo(expected));
        }

        public class SelfReferentialObj
        {
            public SelfReferentialObj()
            {
                SelfReference = this;
            }

            public object SelfReference;
            public int Number = 5;
        }

        [Test, Timeout(200)]
        public void ReferenceLoopsReturn()
        {
            var args = new object[]
            {
                new SelfReferentialObj()
            };
            var paramInfo = InvocationLogUtilTest_Hepler.CreateParameters("param1");

            logUtil.AppendArgumentsJson(stringBuilder, paramInfo, args);

            Assert.That(stringBuilder.ToString(), Does.Contain(@"""Number"": 5"));
            Assert.That(stringBuilder.ToString(), Does.Contain("Self referencing loop detected"));
            Assert.That(stringBuilder.ToString(), Does.Contain(nameof(SelfReferentialObj)));
            Assert.That(stringBuilder.ToString(),
                Does.Contain(nameof(SelfReferentialObj.SelfReference)));
        }
    }
}

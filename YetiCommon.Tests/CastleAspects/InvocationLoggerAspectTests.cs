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
using NUnit.Framework;
using System.Text;
using TestsCommon.TestSupport;
using YetiCommon.CastleAspects;
using YetiCommon.Logging;

namespace YetiCommon.Tests.CastleAspects
{
    [TestFixture]
    public class InvocationLoggerAspectTests
    {
        public class DummyObj
        {
            public virtual void RefParam(ref int number)
            {
                number++;
            }

            public virtual void OutParam(out int number, out object obj)
            {
                number = 9;
                obj = new object();
            }
        }

        LogSpy logSpy;

        InvocationLogUtil logUtil;
        StringBuilder stringBuilder;
        ProxyGenerator proxyGenerator;
        InvocationLoggerAspect aspect;
        DummyObj proxy;

        [SetUp]
        public void SetUp()
        {
            logSpy = new LogSpy();
            logSpy.Attach();

            logUtil = new InvocationLogUtil();
            stringBuilder = new StringBuilder();
            proxyGenerator = new ProxyGenerator();
            aspect = new InvocationLoggerAspect(logUtil);
            proxy = proxyGenerator.CreateClassProxyWithTarget(new DummyObj(), aspect);
        }

        [TearDown]
        public void TearDown()
        {
            logSpy.Detach();
        }

        [Test]
        public void RefParam()
        {
            var number = 55;
            proxy.RefParam(ref number);

            Assert.That(logSpy.GetOutput(), Does.Contain("number=55"));
            Assert.That(logSpy.GetOutput(), Does.Contain("\"number\": 55"));
        }

        [Test]
        public void InitializedOutParam()
        {
            int number = 55;
            object obj = new object();
            proxy.OutParam(out number, out obj);

            Assert.That(logSpy.GetOutput(), Does.Contain("number=55"));
            Assert.That(logSpy.GetOutput(), Does.Contain("obj=System.Object"));
            Assert.That(logSpy.GetOutput(), Does.Contain("55,"));
            Assert.That(logSpy.GetOutput(), Does.Contain("{}"));
        }

        [Test]
        public void UninitializedOutParam()
        {
            int number;
            object obj;
            proxy.OutParam(out number, out obj);

            Assert.That(logSpy.GetOutput(), Does.Contain("number=0"));
            Assert.That(logSpy.GetOutput(), Does.Contain("obj=null"));
            Assert.That(logSpy.GetOutput(), Does.Contain("\"number\": 0"));
            Assert.That(logSpy.GetOutput(), Does.Contain("\"obj\": null"));
        }
    }
}

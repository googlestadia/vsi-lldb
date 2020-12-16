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
using System;
using TestsCommon.TestSupport;
using YetiCommon.CastleAspects;
using YetiCommon.Tests.CastleAspects.TestSupport;

namespace YetiCommon.Tests.CastleAspects
{
    [TestFixture]
    public class LogExceptionAspectTests
    {
        public class ExceptionThrowingDomainObject : IDummyObject
        {
            Exception exception;

            public ExceptionThrowingDomainObject(Exception ex)
            {
                exception = ex;
            }

            #region IDummyObject

            public int GetValue()
            {
                throw exception;
            }

            public void SetValue(int val)
            {
                throw exception;
            }

            #endregion
        }

        ProxyGenerator proxyGenerator;
        LogExceptionAspect aspect;
        IDummyObject proxy;
        LogSpy logSpy;

        [SetUp]
        public void SetUp()
        {
            proxyGenerator = new ProxyGenerator();
            aspect = new LogExceptionAspect();
            proxy = (IDummyObject)proxyGenerator.CreateInterfaceProxyWithTarget(
                    typeof(IDummyObject),
                    new ExceptionThrowingDomainObject(new NotSupportedException("FOOBAR")),
                    aspect);

            logSpy = new LogSpy();
            logSpy.Attach();
        }

        [TearDown]
        public void TearDown()
        {
            logSpy.Detach();
        }

        [Test]
        public void ExceptionIsLogged()
        {
            Assert.Throws<NotSupportedException>(() =>
            {
                proxy.GetValue();
            });
            Assert.That(logSpy.GetOutput(), Does.Contain("FOOBAR"));
            Assert.That(logSpy.GetOutput(), Does.Contain("NotSupportedException"));
            Assert.That(logSpy.GetOutput(), Does.Contain(
                "YetiCommon.Tests.CastleAspects.TestSupport.IDummyObject"));
        }
    }
}

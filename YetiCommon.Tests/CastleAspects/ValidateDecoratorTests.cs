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
using YetiCommon.CastleAspects;
using YetiCommon.Tests.CastleAspects.TestSupport;

namespace YetiCommon.Tests.CastleAspects
{
    [TestFixture]
    class ValidateDecoratorTests
    {
        ProxyGenerator proxyGenerator;
        ProxyGenerationOptions defaultOptions;
        IDummyObject dummyObject;

        [SetUp]
        public void SetUp()
        {
            proxyGenerator = new ProxyGenerator();
            defaultOptions = new ProxyGenerationOptions();
            dummyObject = new DummyObject();
        }

        [Test]
        public void ShouldNotModifyObjectWhenDecoratingValidClassArgument()
        {
            IDecorator classUnderTest = new ValidateDecorator(proxyGenerator, defaultOptions);

            var proxy = classUnderTest.Decorate(dummyObject);

            Assert.AreSame(dummyObject, proxy);
        }

        [Test]
        public void ShouldNotModifyObjectWhenDecoratingValidInterfaceArgument()
        {
            IDecorator classUnderTest = new ValidateDecorator(proxyGenerator, defaultOptions);

            var proxy = classUnderTest.Decorate(dummyObject);

            Assert.AreSame(dummyObject, proxy);
        }

        [Test]
        public void ShouldThrowWhenDecoratingInvalidTarget()
        {
            var invalidateAllTargets = new ProxyGenerationOptions(new InvalidProxyHook());
            IDecorator classUnderTest = new ValidateDecorator(proxyGenerator, invalidateAllTargets);

            Assert.Throws<ArgumentException>(() => classUnderTest.Decorate(dummyObject));
        }

        [Test]
        public void ShouldReturnNullWhenDecoratingNull()
        {
            IDecorator classUnderTest = new ValidateDecorator(proxyGenerator, defaultOptions);
            var shouldBeNull = classUnderTest.Decorate((DummyObject)null);

            Assert.AreEqual(null, shouldBeNull);
        }
    }
}
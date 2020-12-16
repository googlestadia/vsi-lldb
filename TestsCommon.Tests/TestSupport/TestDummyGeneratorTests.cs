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
using TestsCommon.TestSupport;

namespace TestsCommon.Tests.TestSupport
{
    [TestFixture]
    public class TestDummyGeneratorTests
    {
        public interface IDomainObj
        {
            int DoSomething();
        }

        public class DomainObj : IDomainObj
        {
            public virtual int DoSomething()
            {
                return 17;
            }

            public virtual int DoSomethingElse()
            {
                return 23;
            }
        }

        public class DomainObjWithNonVirtualMethod
        {
            public void NonVirtualMethod() { }
        }

        [Test]
        public void InterfaceDummy()
        {
            var dummy = TestDummyGenerator.Create<IDomainObj>();

            Assert.That(dummy, Is.Not.Null);
            Assert.Throws<TestDummyException>(() => dummy.DoSomething());
        }

        [Test]
        public void ClassDummy()
        {
            var dummy = TestDummyGenerator.Create<DomainObj>();

            Assert.That(dummy, Is.Not.Null);
            Assert.Throws<TestDummyException>(() => dummy.DoSomething());
            Assert.Throws<TestDummyException>(() => dummy.DoSomethingElse());
        }

        [Test]
        public void ClassWithNonVirtualFunction()
        {
            Assert.Throws<ArgumentException>(
                () => TestDummyGenerator.Create<DomainObjWithNonVirtualMethod>());
        }
    }
}

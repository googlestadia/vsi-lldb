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
using YetiCommon.CastleAspects;

namespace YetiCommon.Tests.CastleAspects
{
    [TestFixture]
    public class SimpleDecoratorSelfTests
    {
        public interface ISelfType { }

        public interface ISelfType2 { }

        public class TargetObj : SimpleDecoratorSelf<ISelfType>, ISelfType { }

        public class NotATargetObj : ISelfType { }

        public class TargetObj2 : SimpleDecoratorSelf<ISelfType2> { }

        [Test]
        public void SelfAssignedInConstructor()
        {
            var target = new TargetObj();

            Assert.That(target.Self, Is.Not.Null);
            Assert.That(target, Is.SameAs(target.Self));
        }

        [Test]
        public void SelfAssignmentFailsWhenTargetObjIsNotSelfType()
        {
            Assert.Throws<InvalidCastException>(() => new TargetObj2());
        }

        [Test]
        public void AssignSelfAfterConstruction()
        {
            var target = new TargetObj();
            var wrapper = new NotATargetObj();
            target.Self = wrapper;
            Assert.That(target.Self, Is.Not.Null);
            Assert.That(wrapper, Is.SameAs(target.Self));
        }
    }
}

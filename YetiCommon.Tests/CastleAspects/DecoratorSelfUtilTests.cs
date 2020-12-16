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
using YetiCommon.Tests.CastleAspects.TestSupport;

namespace YetiCommon.Tests.CastleAspects
{
    [TestFixture]
    class DecoratorSelfUtilTests
    {
        bool TrySetSelf(object target, object proxy)
        {
            return DecoratorSelfUtil.TrySetSelf(target, proxy);
        }

        [Test]
        public void NullTargetThrowsArgumentNullException()
        {
            TargetObj target = null;
            SelfType proxy = new SelfType();
            Assert.Throws<ArgumentNullException>(() => TrySetSelf(target, proxy));
        }

        [Test]
        public void NullProxyThrowsArgumentNullException()
        {
            TargetObj target = new TargetObj();
            SelfType proxy = null;
            Assert.Throws<ArgumentNullException>(() => TrySetSelf(target, proxy));
        }

        [Test]
        public void NonIDecoratorSelfTargetDoesntThrow()
        {
            object target = new object();
            SelfType proxy = new SelfType();
            Assert.False(TrySetSelf(target, proxy));
        }

        [Test]
        public void NonAssignableProxyDoesntThrow()
        {
            TargetObj target = new TargetObj();
            object proxy = new object();
            Assert.False(TrySetSelf(target, proxy));
        }

        [Test]
        public void ValidAssignWorks()
        {
            TargetObj target = new TargetObj();
            SelfType proxy = new SelfType();
            Assert.True(TrySetSelf(target, proxy));
            Assert.That(target.Self, Is.SameAs(proxy));
        }
    }
}

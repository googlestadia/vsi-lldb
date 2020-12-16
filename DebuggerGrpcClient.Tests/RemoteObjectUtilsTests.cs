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

﻿using System;
using NUnit.Framework;
using NSubstitute;
using System.Collections.Generic;

namespace DebuggerGrpcClient.Tests
{
    public class RemoteObjectFake
    {
        public interface Factory
        {
            RemoteObjectFake Create(FakeHandle handle);

            void Delete(FakeHandle handle);
        }
    }

    public class FakeHandle { };

    public class CreateExceptionFake : Exception { };

    public class DeleteExceptionFake : Exception { };

    [TestFixture]
    [Timeout(5000)]
    class RemoteObjectUtilsTests
    {
        FakeHandle handle1, handle2, handle3;
        List<FakeHandle> handles;

        RemoteObjectFake obj1, obj2, obj3;

        RemoteObjectFake.Factory factory;

        [SetUp]
        public void SetUp()
        {
            factory = Substitute.For<RemoteObjectFake.Factory>();

            handle1 = new FakeHandle();
            handle2 = new FakeHandle();
            handle3 = new FakeHandle();
            handles = new List<FakeHandle> { handle1, handle2, handle3, null };

            obj1 = new RemoteObjectFake();
            factory.Create(handle1).Returns(obj1);

            obj2 = new RemoteObjectFake();
            factory.Create(handle2).Returns(obj2);

            obj3 = new RemoteObjectFake();
            factory.Create(handle3).Returns(obj3);

            factory.Create(null).Returns(_ => { throw new ArgumentNullException(); });
            factory.When(x => x.Delete(null)).Do(_ => { throw new ArgumentNullException(); });
        }

        [Test]
        public void CreateRemoteObjectsSuccess()
        {
            var remoteObjects = RemoteObjectUtils.CreateRemoteObjects(
                factory.Create,
                factory.Delete,
                handles);

            CollectionAssert.AreEqual(new[] { obj1, obj2, obj3, null }, remoteObjects);
        }

        [Test]
        public void CreateRemoteObjectsThrowsErrorAndEitherCreatesOrDeletesAllObjects()
        {
            factory.Create(handle2).Returns(_ => { throw new CreateExceptionFake(); });

            Assert.Throws<CreateExceptionFake>(() => RemoteObjectUtils.CreateRemoteObjects(
                factory.Create, factory.Delete, handles));

            factory.Received().Create(handle1);
            factory.DidNotReceive().Delete(handle1);

            factory.Received().Create(handle2);
            factory.Received().Delete(handle2);

            factory.DidNotReceive().Create(handle3);
            factory.Received().Delete(handle3);
        }

        [Test]
        public void CreateRemoteObjectsCatchesDeleteException()
        {
            factory.Create(handle1).Returns(_ => { throw new CreateExceptionFake(); });
            factory.When(x => x.Delete(handle2)).Do(_ => { throw new DeleteExceptionFake(); });

            Assert.Throws<CreateExceptionFake>(() => RemoteObjectUtils.CreateRemoteObjects(
                factory.Create, factory.Delete, handles));

            factory.Received().Create(handle1);
            factory.Received().Delete(handle1);

            factory.DidNotReceive().Create(handle2);
            factory.Received().Delete(handle2);

            factory.DidNotReceive().Create(handle3);
            factory.Received().Delete(handle3);
        }

        [Test]
        public void CreateRemoteObjectsWhenCreateReturnsNull()
        {
            factory.Create(handle1).Returns((RemoteObjectFake)null);

            Assert.Throws<InvalidOperationException>(() => RemoteObjectUtils.CreateRemoteObjects(
                factory.Create, factory.Delete, handles));

            factory.Received().Create(handle1);
            factory.Received().Delete(handle1);

            factory.DidNotReceive().Create(handle2);
            factory.Received().Delete(handle2);

            factory.DidNotReceive().Create(handle3);
            factory.Received().Delete(handle3);
        }
    }
}

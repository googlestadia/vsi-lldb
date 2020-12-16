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

using Grpc.Core;
using NUnit.Framework;
using System.Collections.Generic;

namespace DebuggerGrpcServer.Tests
{
    [TestFixture]
    [Timeout(5000)]
    class UniqueObjectStoreTests
    {
        class StorageObject
        {
            public int Value { get; }

            public StorageObject(int value)
            {
                Value = value;
            }
        }

        class StorageObjectEqualityComparer : IEqualityComparer<StorageObject>
        {
            public bool Equals(StorageObject x, StorageObject y)
            {
                return x.Value == y.Value;
            }

            public int GetHashCode(StorageObject obj)
            {
                return obj.Value.GetHashCode();
            }
        }

        StorageObjectEqualityComparer _comparer;
        UniqueObjectStore<StorageObject> _store;

        [SetUp]
        public void SetUp()
        {
            _comparer = new StorageObjectEqualityComparer();
            _store = new UniqueObjectStore<StorageObject>(_comparer);
        }

        [Test]
        public void AddDifferentObjects()
        {
            StorageObject obj1 = new StorageObject(1);
            StorageObject obj2 = new StorageObject(2);
            Assert.False(_comparer.Equals(obj1, obj2));

            long id1 = _store.AddObject(obj1);
            long id2 = _store.AddObject(obj2);
            Assert.That(id1, Is.Not.EqualTo(id2));
            Assert.That(_store.Count, Is.EqualTo(2));

            Assert.That(_store.GetObject(id1), Is.EqualTo(obj1));
            Assert.That(_store.GetObject(id2), Is.EqualTo(obj2));

            _store.RemoveObject(id1);
            Assert.Throws<RpcException>(() => _store.GetObject(id1));
            Assert.That(_store.Count, Is.EqualTo(1));

            _store.RemoveObject(id2);
            Assert.Throws<RpcException>(() => _store.GetObject(id2));
            Assert.That(_store.Count, Is.EqualTo(0));
        }

        [Test]
        public void AddEqualObjects()
        {
            StorageObject obj1 = new StorageObject(1);
            StorageObject obj2 = new StorageObject(1);
            Assert.False(obj1 == obj2);
            Assert.True(_comparer.Equals(obj1, obj2));

            long id1 = _store.AddObject(obj1);
            long id2 = _store.AddObject(obj2);
            Assert.That(id1, Is.EqualTo(id2));
            Assert.That(_store.Count, Is.EqualTo(1));

            Assert.That(_store.GetObject(id1), Is.EqualTo(obj1));
            Assert.That(_comparer.Equals(_store.GetObject(id1), obj2));

            _store.RemoveObject(id1);
            Assert.That(_store.GetObject(id2), Is.EqualTo(obj1));
            Assert.That(_store.Count, Is.EqualTo(1));

            _store.RemoveObject(id2);
            Assert.Throws<RpcException>(() => _store.GetObject(id2));
            Assert.That(_store.Count, Is.EqualTo(0));
        }

        [Test]
        public void GetRemoveUnknownObjectThrows()
        {
            // This is a really bad id!
            long badId = 1;
            Assert.Throws<RpcException>(() => _store.GetObject(badId));
            Assert.Throws<RpcException>(() => _store.RemoveObject(badId));
        }
    }
}
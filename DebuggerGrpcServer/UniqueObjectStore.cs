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
using System.Collections.Generic;
using System.Diagnostics;

namespace DebuggerGrpcServer
{
    /// <summary>
    /// Store that allows objects to be tracked by an integer ID. Similar to ObjectStore, except
    /// that objects are deduplicated based on an equality comparer. That means, if two objects
    /// are added that compare equal, they are assigned the same ID. The store keeps track of the
    /// ref count for each object.
    /// </summary>
    public class UniqueObjectStore<T>
    {
        class ObjRef
        {
            public T Obj { get; }
            public long RefCount = 1;

            public ObjRef(T obj)
            {
                Obj = obj;
            }
        }

        readonly Dictionary<long, ObjRef> _objects;
        readonly Dictionary<T, long> _objectIds;
        readonly object _lockObj;
        long _nextId;

        public UniqueObjectStore(IEqualityComparer<T> equalityComparer)
        {
            _objects = new Dictionary<long, ObjRef>();
            _objectIds = new Dictionary<T, long>(equalityComparer);
            _lockObj = new object();
        }

        public int Count
        {
            get
            {
                lock (_lockObj)
                {
                    return _objects.Count;
                }
            }
        }

        /// <summary>
        /// Gets the object referenced by id.
        /// Throws RpcException if the object does not exist.
        /// </summary>
        public T GetObject(long id)
        {
            lock (_lockObj)
            {
                ObjRef objRef;
                if (_objects.TryGetValue(id, out objRef))
                {
                    return objRef.Obj;
                }

                ErrorUtils.ThrowError(StatusCode.Internal,
                    "Could not get " + typeof(T) + " with id : " + id);
                return default(T);
            }
        }

        /// <summary>
        /// Adds obj to the store and returns an id to it. If the object already exists (based on
        /// the equalityComparer passed to the constructor), its ref count is increased and the
        /// same id is returned as for the original object.
        /// </summary>
        public long AddObject(T obj)
        {
            lock (_lockObj)
            {
                long id;
                if (_objectIds.TryGetValue(obj, out id))
                {
                    _objects[id].RefCount++;
                    return id;
                }

                id = ++_nextId;
                _objectIds[obj] = id;
                _objects[id] = new ObjRef(obj);
                return id;
            }
        }

        /// <summary>
        /// Decreases the ref count of the object referenced by id. If nothing references the
        /// object anymore, it is removed from the store.
        /// Throws RpcException if the object does not exist.
        /// </summary>
        public void RemoveObject(long id)
        {
            lock (_lockObj)
            {
                ObjRef objRef;
                if (!_objects.TryGetValue(id, out objRef))
                {
                    ErrorUtils.ThrowError(StatusCode.Internal,
                        "Could not remove " + typeof(T) + " with id : " + id);
                    return;
                }

                Debug.Assert(objRef.RefCount > 0);
                if (--objRef.RefCount == 0)
                {
                    _objects.Remove(id);
                    _objectIds.Remove(objRef.Obj);
                }
            }
        }
    }
}
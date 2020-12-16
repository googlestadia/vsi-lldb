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
using System.Collections.Concurrent;
using System.Threading;

namespace DebuggerGrpcServer
{
    public class ObjectStore<T>
    {
        readonly ConcurrentDictionary<long, T> _objects;
        long _nextId;

        public ObjectStore()
        {
            _objects = new ConcurrentDictionary<long, T>();
            _nextId = 0;
        }

        public int Count => _objects.Count;

        public T GetObject(long id)
        {
            T obj;
            if (_objects.TryGetValue(id, out obj))
            {
                return obj;
            }
            ErrorUtils.ThrowError(StatusCode.Internal,
                                 "Could not get " + typeof(T) + " with id : " + id);
            return default(T);
        }

        public long AddObject(T obj)
        {
            long id = Interlocked.Increment(ref _nextId);
            if (_objects.TryAdd(id, obj))
            {
                return id;
            }
            ErrorUtils.ThrowError(StatusCode.Internal,
                                 "Could not store " + typeof(T) + " with id : " + id);
            return 0;
        }

        public void RemoveObject(long id)
        {
            T obj;
            if (!_objects.TryRemove(id, out obj))
            {
                ErrorUtils.ThrowError(StatusCode.Internal,
                                     "Could not remove " + typeof(T) + " with id : " + id);
            }
        }
    }
}
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
using Grpc.Core.Interceptors;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

namespace DebuggerGrpcClient
{
    // Creates GrpcConnection objects.
    public class GrpcConnectionFactory
    {
        readonly Interceptor[] _interceptors;
        readonly JoinableTaskFactory _taskFactory;

        [Obsolete("This constructor only exists to support mocking libraries.", error: true)]
        public GrpcConnectionFactory() : this(new JoinableTaskContext().Factory, new Interceptor[0])
        {
        }

        public GrpcConnectionFactory(JoinableTaskFactory taskFactory,
                                     params Interceptor[] interceptors)
        {
            _taskFactory = taskFactory ?? throw new ArgumentNullException(nameof(taskFactory));
            _interceptors = interceptors ?? throw new ArgumentNullException(nameof(interceptors));
        }

        public GrpcConnection Create(CallInvoker callInvoker) =>
            new GrpcConnection(_taskFactory, callInvoker, _interceptors);
    }

    /// <summary>
    /// Class for batch deleting a list of objects of generic type T.
    /// </summary>
    public class BulkDeleter<T>
    {
        readonly List<T> _objectsToDelete = new List<T>();
        readonly int _batchSize;

        public BulkDeleter(int batchSize)
        {
            _batchSize = batchSize;
        }

        public void QueueForDeletion(T obj, Action<List<T>> deleter)
        {
            List<T> deleteList = null;

            lock (_objectsToDelete)
            {
                _objectsToDelete.Add(obj);
                if (_objectsToDelete.Count >= _batchSize)
                {
                    deleteList = new List<T>(_objectsToDelete);
                    _objectsToDelete.Clear();
                }
            }

            if (deleteList != null)
            {
                deleter(deleteList);
            }
        }
    }

    /// <summary>
    /// Base class for implementing APIs over GPRC with common error handling.
    /// The number of concurrent connections may be limited by the |baseCallInvoker| passed into
    /// the constructor, see documentation of the corresponding proper type. Concurrency is also
    /// limited by what the server can handle. Some calls like expression evaluation currently
    /// acquire a global lock.
    /// </summary>
    public class GrpcConnection
    {
        public event Action<RpcException> RpcException;
        public event Action AsyncRpcCompleted;

        public CallInvoker CallInvoker { get; }

        readonly JoinableTaskFactory _taskFactory;

        JoinableTask _asyncGrpcTask;
        readonly SemaphoreSlim _asyncGrpcSemaphore = new SemaphoreSlim(1, 1);

        volatile int _shutdown = 0;

        // Objects are of type BulkDeleter<Type>.
        readonly ConcurrentDictionary<Type, object> _bulkDeleters =
            new ConcurrentDictionary<Type, object>();

        const int _defaultBulkDeleteBatchSize = 100;
        public int BulkDeleteBatchSize { get; set; }

        public GrpcConnection(JoinableTaskFactory taskFactory, CallInvoker callInvoker,
                              params Interceptor[] interceptors)
        {
            _taskFactory = taskFactory;
            BulkDeleteBatchSize = _defaultBulkDeleteBatchSize;
            CallInvoker = callInvoker.Intercept(interceptors);
        }

        public void Shutdown()
        {
            _shutdown = 1;
        }

        /// <summary>
        /// Invokes |action| synchronously and returns true on success. |action| is assumed to be
        /// an rpc using this GrpcConnection's CallInvoker. Note that the CallInvoker might
        /// restrict the number of threads that can perform rpc concurrently, see e.g.
        /// PipeCallInvokerFactory.
        /// Calls on the main thread never run concurrently with asynchronous grpc calls, the
        /// method blocks until the current async call is completed.
        /// </summary>
        public bool InvokeRpc(Action action)
        {
            // TODO (internal) Review the calls made from background thread and make them use
            // InvokeBackgroundRpc
            // TODO (internal)  Make CancelableTask remain on the main thread by default
            // TODO (internal) Once the above two tasks are resolved, throw in the beginning of
            // this method if not on the main thread.
            if (!_taskFactory.Context.IsOnMainThread)
            {
                return DoInvokeSynchronousRpc(action);
            }

            try
            {
                _asyncGrpcTask?.Join();
            }
            catch (Exception)
            {
                // The exception will be thrown from InvokeRpcAsync and should not be processed in
                // other places.
            }

            return DoInvokeSynchronousRpc(action);
        }

        /// <summary>
        /// Invoke synchronous grpc call. The method should be used when on background thread.
        /// The call might be executed concurrently with the sync and async calls run on the
        /// main thread.
        /// </summary>
        public bool InvokeBackgroundRpc(Action action)
        {
            return DoInvokeSynchronousRpc(action);
        }

        bool DoInvokeSynchronousRpc(Action action)
        {
            if (_shutdown != 0)
            {
                return false;
            }

            try
            {
                action();
                return true;
            }
            catch (RpcException e)
            {
                if (_shutdown == 0)
                {
                    Trace.WriteLine(e.ToString());
                    RpcException?.Invoke(e);
                }

                return false;
            }
            catch (ObjectDisposedException) when (_shutdown != 0)
            {
                // Racy RPC call during shutdown.
                return false;
            }
        }

        /// <summary>
        /// Invokes |task| asynchronously. |task| is assumed to perform an asynchronous rpc using
        /// this GrpcConnection's CallInvoker. Note that the CallInvoker might restrict the number
        /// of threads that can perform rpc concurrently, see e.g. PipeCallInvokerFactory.
        /// </summary>
        public async Task<bool> InvokeRpcAsync(Func<Task> task)
        {
            await _taskFactory.SwitchToMainThreadAsync();

            if (_shutdown != 0)
            {
                return false;
            }

            try
            {
                // We do not use JoinAsync because it implicitly makes assumptions that only two
                // async calls can be invoked at the same time. But we can have one call in
                // progress, one call asynchronously joining on it, and at that moment another
                // call can arrive.
                await _asyncGrpcSemaphore.WaitAsync();
                _asyncGrpcTask = _taskFactory.RunAsync(task);
                await _asyncGrpcTask;
                AsyncRpcCompleted?.Invoke();
                return true;
            }
            catch (RpcException e)
            {
                if (_shutdown == 0)
                {
                    Trace.WriteLine(e.ToString());
                    RpcException?.Invoke(e);
                }

                return false;
            }
            catch (ObjectDisposedException) when (_shutdown != 0)
            {
                // Racy RPC call during shutdown.
                return false;
            }
            finally
            {
                _asyncGrpcTask = null;
                _asyncGrpcSemaphore.Release();
            }
        }

        /// <summary>
        /// Gets a bulk deleter for objects of type |T|. It is used to batch delete objects.
        /// </summary>
        public BulkDeleter<T> GetOrCreateBulkDeleter<T>()
        {
            return (BulkDeleter<T>) _bulkDeleters.GetOrAdd(typeof(T),
                                                           t => new BulkDeleter<T>(
                                                               BulkDeleteBatchSize));
        }
    }
}
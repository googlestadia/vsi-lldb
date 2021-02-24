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

using DebuggerGrpc;
using Grpc.Core;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DebuggerGrpcClient
{
    public class PipeCallInvokerFactory
    {
        /// <summary>
        /// Number of Grpc "channels" that can be used concurrently. For optimal performance, there
        /// should be one for each thread that makes Grpc calls. Right now, there are 4 or more
        /// different threads that make rpc calls:
        /// - The main thread
        /// - The GC Finalizer thread
        /// - The event manager thread (in particular calls WaitForEvent, blocking for a second)
        /// - Asynchronous tasks during startup (though they don't seem to overlap)
        /// In practice, there are often 2 concurrent rpc calls, rarely 3, pretty much never 4.
        /// </summary>
        const int NUM_GRPC_PIPE_PAIRS = 4;

        public virtual PipeCallInvoker Create()
        {
            return new PipeCallInvoker(NUM_GRPC_PIPE_PAIRS);
        }
    }

    /// <summary>
    /// Call invoker that uses anonymous pipes to send rpc messages. Multiple threads can send
    /// messages concurrently, depending on the number of pipe pairs.
    /// </summary>
    public class PipeCallInvoker : CallInvoker, IDisposable
    {
        readonly PipePair[] pipePairs;
        readonly ConcurrentBag<PipePair> availablePipePairs;
        readonly SemaphoreSlim pipeLock;

        public PipeCallInvoker(int numPipePairs)
        {
            pipePairs = new PipePair[numPipePairs];
            availablePipePairs = new ConcurrentBag<PipePair>();
            for (int n = 0; n < numPipePairs; ++n)
            {
                pipePairs[n] = new PipePair();
                availablePipePairs.Add(pipePairs[n]);
            }

            // Allow pipePairs.Length threads to run concurrently, so that each one can pick its
            // own pipe.
            pipeLock = new SemaphoreSlim(pipePairs.Length, pipePairs.Length);
        }

        ~PipeCallInvoker()
        {
            Dispose(false);
        }

        void Dispose(bool disposing)
        {
            if (disposing)
            {
                pipeLock.Dispose();

                // Disposing the pipes will cause an EndOfStreamException on the server, which it
                // interprets as client shutdown.
                foreach (PipePair pipePair in pipePairs)
                {
                    pipePair.InPipe.Dispose();
                    pipePair.OutPipe.Dispose();
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Returns the client handles as strings for all input and output pipes.
        /// </summary>
        public void GetClientPipeHandles(out string[] inPipeClientHandles,
                                         out string[] outPipeClientHandles)
        {
            inPipeClientHandles =
                pipePairs.Select(pp => pp.InPipe.GetClientHandleAsString()).ToArray();
            outPipeClientHandles =
                pipePairs.Select(pp => pp.OutPipe.GetClientHandleAsString()).ToArray();
        }

        /// <summary>
        /// Disposes the local copies of the client pipe handles. Must be called after starting the
        /// DebuggerGrpcServer process, so that the pipes in this process are closed when the
        /// server shuts down.
        /// </summary>
        public virtual void DisposeLocalCopyOfClientPipeHandles()
        {
            foreach (PipePair pipePair in pipePairs)
            {
                pipePair.InPipe.DisposeLocalCopyOfClientHandle();
                pipePair.OutPipe.DisposeLocalCopyOfClientHandle();
            }
        }

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string host, CallOptions options, TRequest request)
        {
            // Wait for an available pipe.
            pipeLock.Wait();
            PipePair pipePair;
            availablePipePairs.TryTake(out pipePair);
            Debug.Assert(pipePair != null);
            try
            {
                // Create binary reader/writer, but be sure to leave the stream open!
                var writer = new BinaryWriter(pipePair.OutPipe, Encoding.Default, true);
                var reader = new BinaryReader(pipePair.InPipe, Encoding.Default, true);

                // Send the RPC name.
                writer.Write(method.FullName.Length);
                writer.Write(Encoding.ASCII.GetBytes(method.FullName));

                // Send the request.
                using (var serializationContext = new SimpleSerializationContext())
                {
                    method.RequestMarshaller.ContextualSerializer(request, serializationContext);
                    byte[] requestBytes = serializationContext.GetPayload();
                    writer.Write(requestBytes.Length);
                    writer.Write(requestBytes);
                }

                // Read the response.
                int size = reader.ReadInt32();
                byte[] responseBytes = reader.ReadBytes(size);
                var deserializationContext = new SimpleDeserializationContext(responseBytes);
                return method.ResponseMarshaller.ContextualDeserializer(deserializationContext);
            }
            // Unfortunately, RpcExceptions can't be nested with InnerException.
            catch (EndOfStreamException e)
            {
                throw new RpcException(new Status(StatusCode.Unknown, e.ToString()),
                                       "Connection to server lost. Did it shut down?");
            }
            catch (Exception e) when (!(e is RpcException))
            {
                throw new RpcException(new Status(StatusCode.Unknown, e.ToString()),
                                       "Unknown failure: " + e);
            }
            finally
            {
                availablePipePairs.Add(pipePair);
                pipeLock.Release();
            }
        }

        async Task<TResponse> AsyncCallAsync<TRequest, TResponse>(
            Method<TRequest, TResponse> method, TRequest request)
        {
            await pipeLock.WaitAsync();
            PipePair pipePair;
            availablePipePairs.TryTake(out pipePair);
            Debug.Assert(pipePair != null);
            try
            {
                var writer = new AsyncBinaryWriter(pipePair.OutPipe);
                var reader = new AsyncBinaryReader(pipePair.InPipe);

                // Send the RPC name.
                await writer.WriteAsync(method.FullName.Length);
                await writer.WriteAsync(Encoding.ASCII.GetBytes(method.FullName));

                // Send the request.
                using (var serializationContext = new SimpleSerializationContext())
                {
                    method.RequestMarshaller.ContextualSerializer(request, serializationContext);
                    byte[] requestBytes = serializationContext.GetPayload();
                    await writer.WriteAsync(requestBytes.Length);
                    await writer.WriteAsync(requestBytes);
                }

                // Read the response.
                int size = await reader.ReadInt32Async();
                byte[] responseBytes = await reader.ReadBytesAsync(size);
                var context = new SimpleDeserializationContext(responseBytes);
                return method.ResponseMarshaller.ContextualDeserializer(context);
            }
            // Unfortunately, RpcExceptions can't be nested with InnerException.
            catch (EndOfStreamException e)
            {
                throw new RpcException(new Status(StatusCode.Unknown, e.ToString()),
                                       "Connection to server lost. Did it shut down?");
            }
            catch (Exception e) when (!(e is RpcException))
            {
                throw new RpcException(new Status(StatusCode.Unknown, e.ToString()),
                                       "Unknown failure: " + e);
            }
            finally
            {
                availablePipePairs.Add(pipePair);
                pipeLock.Release();
            }
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string host, CallOptions options, TRequest request)
        {
            // Not sure what these are used for, but they never seem to be called.
            Task<Metadata> responseHeadersAsync = Task.FromResult(new Metadata());
            Func<Status> getStatusFunc = () => new Status();
            Func<Metadata> getTrailersFunc = () => new Metadata();
            Action disposeAction = () => { };

            return new AsyncUnaryCall<TResponse>(AsyncCallAsync(method, request),
                                                 responseHeadersAsync, getStatusFunc,
                                                 getTrailersFunc, disposeAction);
        }

        public override AsyncClientStreamingCall<TRequest, TResponse>
            AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
                                                          string host, CallOptions options)
        {
            throw new NotImplementedException();
        }

        public override AsyncDuplexStreamingCall<TRequest, TResponse>
            AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
                                                          string host, CallOptions options)
        {
            throw new NotImplementedException();
        }

        public override AsyncServerStreamingCall<TResponse>
            AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
                                                          string host, CallOptions options,
                                                          TRequest request)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Input/output pipes for Grpc communication.
        /// </summary>
        class PipePair
        {
            // Confusingly, the grpc server is the pipe client and vice versa.
            public AnonymousPipeServerStream InPipe { get; }
            public AnonymousPipeServerStream OutPipe { get; }

            public PipePair()
            {
                InPipe = new AnonymousPipeServerStream(PipeDirection.In,
                                                       HandleInheritability.Inheritable);
                OutPipe = new AnonymousPipeServerStream(PipeDirection.Out,
                                                        HandleInheritability.Inheritable);
            }
        }

        /// <summary>
        /// Asynchronous version of BinaryWriter limited to the functionality needed.
        /// </summary>
        class AsyncBinaryWriter
        {
            readonly AnonymousPipeServerStream pipe;

            public AsyncBinaryWriter(AnonymousPipeServerStream pipe)
            {
                this.pipe = pipe;
            }

            public async Task WriteAsync(int value)
            {
                byte[] bytes = BitConverter.GetBytes(value);

                // The server expects little endian.
                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes);
                }

                await WriteAsync(bytes);
            }

            public async Task WriteAsync(byte[] bytes)
            {
                await pipe.WriteAsync(bytes, 0, bytes.Length);
            }
        }

        /// <summary>
        /// Asynchronous version of BinaryReader limited to the functionality needed.
        /// </summary>
        class AsyncBinaryReader
        {
            readonly AnonymousPipeServerStream pipe;

            public AsyncBinaryReader(AnonymousPipeServerStream pipe)
            {
                this.pipe = pipe;
            }

            public async Task<int> ReadInt32Async()
            {
                byte[] bytes = await ReadBytesAsync(sizeof(int));

                // The server expects little endian.
                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes);
                }

                return BitConverter.ToInt32(bytes, 0);
            }

            public async Task<byte[]> ReadBytesAsync(int size)
            {
                byte[] bytes = new byte[size];
                await pipe.ReadAsync(bytes, 0, bytes.Length);
                return bytes;
            }
        }
    }
}
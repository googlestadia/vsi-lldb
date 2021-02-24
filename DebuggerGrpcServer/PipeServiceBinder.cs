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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.ExceptionServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DebuggerGrpcServer
{
    /// <summary>
    /// Service binder implementation that uses pipes to exchange messages with the client.
    /// Creates a thread for each pair of input/output pipes to handle messages.
    /// </summary>
    public class PipeServiceBinder : ServiceBinderBase
    {
        PipePair[] pipePairs;

        public PipeServiceBinder(string[] inPipeHandles, string[] outPipeHandles)
        {
            Debug.Assert(inPipeHandles.Length == outPipeHandles.Length);
            int numPipes = inPipeHandles.Length;
            pipePairs = new PipePair[numPipes];
            for (int n = 0; n < numPipes; ++n)
            {
                pipePairs[n] = new PipePair(inPipeHandles[n], outPipeHandles[n]);
            }
        }

        public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method,
                                                            UnaryServerMethod<TRequest, TResponse>
                                                                handler)
        {
            IProcessMessage processor = new MethodAndHandler<TRequest, TResponse>(
                requestBytes =>
                {
                    var context = new SimpleDeserializationContext(requestBytes);
                    return method.RequestMarshaller.ContextualDeserializer(context);
                },
                request =>
                {
                    using (var context = new SimpleSerializationContext())
                    {
                        method.ResponseMarshaller.ContextualSerializer(request, context);
                        return context.GetPayload();
                    }
                },
                handler);
            messageProcessors.Add(method.FullName, processor);
        }

        interface IProcessMessage
        {
            byte[] Process(byte[] request);
        }

        class MethodAndHandler<TRequest, TResponse> : IProcessMessage
            where TRequest : class where TResponse : class
        {
            Func<byte[], TRequest> requestDeserializer;
            Func<TResponse, byte[]> responseSerializer;
            UnaryServerMethod<TRequest, TResponse> handler;

            public MethodAndHandler(Func<byte[], TRequest> requestDeserializer,
                                    Func<TResponse, byte[]> responseSerializer,
                                    UnaryServerMethod<TRequest, TResponse> handler)
            {
                this.requestDeserializer = requestDeserializer;
                this.responseSerializer = responseSerializer;
                this.handler = handler;
            }

            public byte[] Process(byte[] requestPayload)
            {
                TRequest request = requestDeserializer(requestPayload);
                Task<TResponse> task = handler(request, null);
                // Wait synchronously here for performance reasons. There are no deadlock issues
                // since the jobs don't switch threads.
#pragma warning disable VSTHRD002
                return responseSerializer(task.Result);
#pragma warning restore VSTHRD002
            }
        }

        Dictionary<string, IProcessMessage> messageProcessors =
            new Dictionary<string, IProcessMessage>();

        public Task ShutdownTask { get; private set; }

        public void Start()
        {
            ShutdownTask = RunAsync();
        }

        async Task RunAsync()
        {
            // Create a task for each pipe pair to handle the rpc requests for that pipe pair.
            List<Task> tasks = new List<Task>();
            foreach (PipePair pipePair in pipePairs)
            {
                // LongRunning creates a thread for each pipe worker. Without it, the task would
                // run on the ThreadPool, which might cause some tasks to run late if the
                // ThreadPool is too small.
                tasks.Add(Task.Factory.StartNew(() => RunPipeWorker(pipePair),
                                                CancellationToken.None,
                                                TaskCreationOptions.LongRunning,
                                                TaskScheduler.Default));
            }

            // The completedTask has to be awaited again to get the exception.
            Task completedTask = await Task.WhenAny(tasks.ToArray());
            try
            {
                await completedTask;

                // Exceptions should be the only way to finish a pipe worker.
                throw new Exception("RPC worker closed for unknown reason.");
            }
            catch (EndOfStreamException)
            {
                // Happens when the client closes the pipes on shutdown.
                Console.WriteLine("Server shutting down as requested by client.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Server shut down unexpectedly. Error: " + ex.ToString());
            }

            foreach (PipePair pipePair in pipePairs)
            {
                pipePair.InPipe.Dispose();
                pipePair.OutPipe.Dispose();
            }
        }

        [HandleProcessCorruptedStateExceptions, SecurityCritical]
        void RunPipeWorker(PipePair pipePair)
        {
            var writer = new BinaryWriter(pipePair.OutPipe, Encoding.ASCII, true);
            var reader = new BinaryReader(pipePair.InPipe, Encoding.ASCII, true);

            // The only way to shut down grpc server right now is by throwing an exception.
            // Note that the pipes throw EndOfStreamException when the client shuts down.
            for (;;)
            {
                // Read the RPC name.
                int rpcNameSize = reader.ReadInt32();
                byte[] rpcNameBytes = reader.ReadBytes(rpcNameSize);
                string rpcName = Encoding.ASCII.GetString(rpcNameBytes);

                // Read the request.
                int requestSize = reader.ReadInt32();
                byte[] requestBytes = reader.ReadBytes(requestSize);

                // Call the handler.
                IProcessMessage processor;
                if (!messageProcessors.TryGetValue(rpcName, out processor))
                {
                    throw new Exception($"Unknown rpc '{rpcName}'");
                }

                byte[] responseBytes = responseBytes = processor.Process(requestBytes);

                // Send the response back.
                writer.Write(responseBytes.Length);
                writer.Write(responseBytes);
            }
        }

        class PipePair
        {
            // Confusingly, the grpc server is the pipe client and vice versa.
            public AnonymousPipeClientStream InPipe { get; }
            public AnonymousPipeClientStream OutPipe { get; }

            public PipePair(string inPipeHandle, string outPipeHandle)
            {
                InPipe = new AnonymousPipeClientStream(PipeDirection.In, inPipeHandle);
                OutPipe = new AnonymousPipeClientStream(PipeDirection.Out, outPipeHandle);
            }
        }
    }
}
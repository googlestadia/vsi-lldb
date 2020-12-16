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
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Win32.SafeHandles;

namespace DebuggerGrpcClient.Tests
{
    [TestFixture]
    [Timeout(5000)]
    class PipeCallInvokerTests
    {
        PipeCallInvoker invoker;

        const int NUM_PIPE_PAIRS = 2;

        AnonymousPipeClientStream[] clientInPipes;
        AnonymousPipeClientStream[] clientOutPipes;

        // For testing, don't send protos, but regular strings as requests and responses.
        Marshaller<string> stringMarshaller;
        Method<string, string> stringMethod;

        [SetUp]
        public void SetUp()
        {
            invoker = new PipeCallInvoker(NUM_PIPE_PAIRS);

            clientInPipes = new AnonymousPipeClientStream[NUM_PIPE_PAIRS];
            clientOutPipes = new AnonymousPipeClientStream[NUM_PIPE_PAIRS];

            stringMarshaller = new Marshaller<string>(str => Encoding.ASCII.GetBytes(str),
                                                      bytes => Encoding.ASCII.GetString(bytes));

            stringMethod = new Method<string, string>(MethodType.Unary, "ServiceName", "MethodName",
                                                      stringMarshaller, stringMarshaller);
        }

        [Test]
        public void DisposeLocalCopyOfClientPipeHandles()
        {
            invoker.DisposeLocalCopyOfClientPipeHandles();

            // The above call should close the underlying pipes, so that the client pipes can't be
            // created from the handles.
            Assert.Throws<IOException>(() => CreateClientPipes());

            invoker.Dispose();
        }

        [Test]
        public void DisposePipes()
        {
            invoker.Dispose();

            // Same here, the underlying pipes should be closed.
            Assert.Throws<IOException>(() => CreateClientPipes());
        }

        [Test]
        public void SyncCall()
        {
            CreateClientPipes();

            Task[] handlerTasks = CreateHandlerTasks(RespondWithMethodRequestResponse);

            const string request = "MyRequest";
            string response =
                invoker.BlockingUnaryCall(stringMethod, null, new CallOptions(), request);
            Assert.That(response, Is.EqualTo($"{stringMethod.FullName} {request} response"));

            Task.WaitAny(handlerTasks);

            // This will cause the remaining task(s) to throw an EndOfStreamException and exit.
            invoker.Dispose();
            Assert.Throws<AggregateException>(() => Task.WaitAll(handlerTasks));
        }

        [Test]
        public async Task AsyncCallAsync()
        {
            CreateClientPipes();

            Task[] handlerTasks = CreateHandlerTasks(RespondWithMethodRequestResponse);

            const string request = "MyRequest";
            string response =
                await invoker.AsyncUnaryCall(stringMethod, null, new CallOptions(), request);
            Assert.That(response, Is.EqualTo($"{stringMethod.FullName} {request} response"));

            Task.WaitAny(handlerTasks);

            // This will cause the remaining task(s) to throw an EndOfStreamException and exit.
            invoker.Dispose();
            Assert.Throws<AggregateException>(() => Task.WaitAll(handlerTasks));
        }

        [Test]
        public async Task SyncDuringAsyncCallAsync()
        {
            CreateClientPipes();

            Task[] handlerTasks = CreateHandlerTasks(RespondWithMethodRequestResponse);

            const string asyncRequest = "MyAsyncRequest";
            AsyncUnaryCall<string> asyncTask =
                invoker.AsyncUnaryCall(stringMethod, null, new CallOptions(), asyncRequest);

            const string syncRequest = "MySyncRequest";
            string syncResponse =
                invoker.BlockingUnaryCall(stringMethod, null, new CallOptions(), syncRequest);

            string asyncResponse = await asyncTask;

            Assert.That(syncResponse,
                        Is.EqualTo($"{stringMethod.FullName} {syncRequest} response"));
            Assert.That(asyncResponse,
                        Is.EqualTo($"{stringMethod.FullName} {asyncRequest} response"));

            // Wait for the handlers to exit. Both should exit after the rpc they handled.
            Task.WaitAll(handlerTasks);
            Assert.That(NUM_PIPE_PAIRS, Is.EqualTo(2));
            Assert.That(handlerTasks[0].IsCompleted);
            Assert.That(handlerTasks[1].IsCompleted);

            invoker.Dispose();
        }

        [Test]
        public void ExceptionsConvertedToRpcException()
        {
            CreateClientPipes();

            Task[] handlerTasks = CreateHandlerTasks(RespondWithMethodRequestResponse);

            // Create a method that will throw when trying to deserialize the response.
            var throwingMarshaller = new Marshaller<string>(str => stringMarshaller.Serializer(str),
                                                            bytes =>
                                                            {
                                                                throw new NotImplementedException();
                                                            });

            var throwingMethod = new Method<string, string>(MethodType.Unary, "ServiceName",
                                                            "MethodName", throwingMarshaller,
                                                            throwingMarshaller);

            Assert.Throws<RpcException>(
                () => invoker.BlockingUnaryCall(throwingMethod, null, new CallOptions(),
                                                "request"));

            // This will cause the remaining task(s) to throw an EndOfStreamException and exit.
            invoker.Dispose();
            Assert.Throws<AggregateException>(() => Task.WaitAll(handlerTasks));
        }

        [Test]
        public void SyncCallsFromManyThreads()
        {
            CreateClientPipes();

            Task[] handlerTasks = CreateHandlerTasks(RespondWithMethodRequestResponse, true);

            const int NUM_SENDERS = 10;
            const int NUM_RUNS_PER_SENDER = 100;
            Task[] senderTasks = new Task[NUM_SENDERS];
            for (int senderIdx = 0; senderIdx < NUM_SENDERS; ++senderIdx)
            {
                // Capture in a local variable.
                string request = $"MyRequest {senderIdx}";
                senderTasks[senderIdx] = Task.Factory.StartNew(() =>
                {
                    for (int runIdx = 0; runIdx < NUM_RUNS_PER_SENDER; ++runIdx)
                    {
                        string response =
                            invoker.BlockingUnaryCall(stringMethod, null, new CallOptions(),
                                                      request);
                        Assert.That(
                            response, Is.EqualTo($"{stringMethod.FullName} {request} response"));
                    }
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }

            Task.WaitAll(senderTasks);

            // This will cause the remaining task(s) to throw an EndOfStreamException and exit.
            invoker.Dispose();
            Assert.Throws<AggregateException>(() => Task.WaitAll(handlerTasks));
        }

        [Test]
        public void LargeSyncCall()
        {
            CreateClientPipes();

            Task[] handlerTasks = CreateHandlerTasks(RespondWithMethodRequestResponse);

            // Make sure that sending large amounts of data won't stall the pipes for some reason.
            string request = new string('R', 1024 * 1024);
            string response =
                invoker.BlockingUnaryCall(stringMethod, null, new CallOptions(), request);
            Assert.That(response, Is.EqualTo($"{stringMethod.FullName} {request} response"));

            Task.WaitAny(handlerTasks);

            // This will cause the remaining task(s) to throw an EndOfStreamException and exit.
            invoker.Dispose();
            Assert.Throws<AggregateException>(() => Task.WaitAll(handlerTasks));
        }

        void CreateClientPipes()
        {
            string[] inHandles, outHandles;
            invoker.GetClientPipeHandles(out inHandles, out outHandles);

            Assert.That(inHandles, Is.Not.Null);
            Assert.That(outHandles, Is.Not.Null);

            Assert.That(inHandles.Length, Is.EqualTo(NUM_PIPE_PAIRS));
            Assert.That(outHandles.Length, Is.EqualTo(NUM_PIPE_PAIRS));

            for (int n = 0; n < NUM_PIPE_PAIRS; ++n)
            {
                // Create NON-OWNED safe handles here. Passing in the pipe handle string into the
                // constructor of AnonymousPipeClientStream would create an owned handle, which
                // would mess up the ref count and cause double-deletion because both client*Pipes
                // and the invoker's pipe streams both assume they own the pipe.
                var outHandle = new SafePipeHandle(new IntPtr(long.Parse(outHandles[n])), false);
                var inHandle = new SafePipeHandle(new IntPtr(long.Parse(inHandles[n])), false);

                // Note: The server's in pipes are the client's out pipes and vice versa.
                clientInPipes[n] = new AnonymousPipeClientStream(PipeDirection.In, outHandle);
                clientOutPipes[n] = new AnonymousPipeClientStream(PipeDirection.Out, inHandle);
            }
        }

        /// <summary>
        /// Creates a fake message handler that responds to a method name "A" and a string request
        /// "B" with a string response "A B response".
        /// </summary>
        void RespondWithMethodRequestResponse(BinaryReader reader, BinaryWriter writer)
        {
            int methodNameSize = reader.ReadInt32();
            byte[] methodNameBytes = reader.ReadBytes(methodNameSize);
            string methodName = Encoding.ASCII.GetString(methodNameBytes);

            int requestBytesSize = reader.ReadInt32();
            byte[] receivedRequestBytes = reader.ReadBytes(requestBytesSize);
            string request = Encoding.ASCII.GetString(receivedRequestBytes);

            string response = $"{methodName} {request} response";
            byte[] responseBytes = Encoding.ASCII.GetBytes(response);
            writer.Write(responseBytes.Length);
            writer.Write(responseBytes);
        }

        Task[] CreateHandlerTasks(Action<BinaryReader, BinaryWriter> handler,
                                  bool keepRunning = false)
        {
            Task[] handlerTasks = new Task[NUM_PIPE_PAIRS];
            for (int n = 0; n < NUM_PIPE_PAIRS; ++n)
            {
                var reader = new BinaryReader(clientInPipes[n], Encoding.ASCII, true);
                var writer = new BinaryWriter(clientOutPipes[n], Encoding.ASCII, true);

                handlerTasks[n] = Task.Factory.StartNew(() =>
                {
                    do
                    {
                        handler(reader, writer);
                    } while (keepRunning);
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }

            return handlerTasks;
        }
    }
}
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

﻿using Debugger.RemoteFrameRpc;
using Debugger.RemoteThreadRpc;
using Debugger.SbModuleRpc;
using DebuggerGrpcClient;
using DebuggerGrpcServer;
using Grpc.Core;
using LldbApi;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.Threading;

namespace DebuggerGrpcClientServer.Tests
{
    /// <summary>
    /// Class that all test fixtures for gRPC client-server tests should inherit from.
    /// It sets up and tears down the server and its services.
    /// Also, it ensures that all the object stores are empty after each test.
    /// </summary>
    abstract class BaseIntegrationTests
    {
        private List<ExceptionDispatchInfo> exceptions;
        private ServerInfo serverInfo;

        protected void BaseSetUp()
        {
            Assert.IsNull(serverInfo);
            var grpcConnectionFactory =
                new GrpcConnectionFactory(new JoinableTaskContext().Factory);
            serverInfo = CreateServer(new PipeCallInvokerFactory(), grpcConnectionFactory);
            exceptions = new List<ExceptionDispatchInfo>();
            // Store RPC exceptions keeping stack trace.
            Connection.RpcException += e => exceptions.Add(ExceptionDispatchInfo.Capture(e));
            // Make sure everything gets deleted immediately. We're checking this on shutdown!
            Connection.BulkDeleteBatchSize = 1;
        }

        protected void BaseTearDown()
        {
            Assert.IsNotNull(serverInfo);
            Connection.Shutdown();
            CallInvoker.Dispose();

            // Temporarily disabling the deprecated method call warning
            // (that method is meant to be called from this class).
            // Also wait synchronously, should be fine in tests and
            // ITestAction doesn't have an async version.
#pragma warning disable 618, VSTHRD002
            Server.ShutdownTask.Wait();
#pragma warning restore 618, VSTHRD002

            try
            {
                Assert.Multiple(() =>
                {
                    Assert.AreEqual(0, ProcessStore.Count);
                    Assert.AreEqual(0, FrameStore.Count);
                    Assert.AreEqual(0, ThreadStore.Count);
                    Assert.AreEqual(0, ValueStore.Count);
                    Assert.AreEqual(0, FunctionStore.Count);
                    Assert.AreEqual(0, ModuleStore.Count);
                    Assert.AreEqual(0, SectionStore.Count);
                    Assert.AreEqual(0, SymbolStore.Count);
                    Assert.IsEmpty(exceptions);
                });
            }
            finally
            {
                exceptions = null;
                serverInfo = null;
            }
        }

        #region Server info aliases

        protected PipeServiceBinder Server => serverInfo.Server;
        protected PipeCallInvoker CallInvoker => serverInfo.CallInvoker;
        protected GrpcConnection Connection => serverInfo.Connection;
        protected ConcurrentDictionary<int, SbProcess> ProcessStore => serverInfo.Stores.Process;
        protected ObjectStore<RemoteFrame> FrameStore => serverInfo.Stores.Frame;
        protected ObjectStore<RemoteThread> ThreadStore => serverInfo.Stores.Thread;
        protected ObjectStore<RemoteValue> ValueStore => serverInfo.Stores.Value;
        protected ObjectStore<SbFunction> FunctionStore => serverInfo.Stores.Function;
        protected UniqueObjectStore<SbModule> ModuleStore => serverInfo.Stores.Module;
        protected ObjectStore<SbSection> SectionStore => serverInfo.Stores.Section;
        protected ObjectStore<SbSymbol> SymbolStore => serverInfo.Stores.Symbol;

        #endregion

        #region Server setup helper types/methods

        private class RemoteObjectStores
        {
            public ConcurrentDictionary<int, SbProcess> Process { get; } =
                new ConcurrentDictionary<int, SbProcess>();
            public ObjectStore<RemoteFrame> Frame { get; } =
                new ObjectStore<RemoteFrame>();
            public ObjectStore<RemoteThread> Thread { get; } =
                new ObjectStore<RemoteThread>();
            public ObjectStore<RemoteValue> Value { get; } =
                new ObjectStore<RemoteValue>();
            public ObjectStore<SbFunction> Function { get; } =
                new ObjectStore<SbFunction>();
            public UniqueObjectStore<SbModule> Module { get; } =
                new UniqueObjectStore<SbModule>(SbModuleEqualityComparer.Instance);
            public ObjectStore<SbSection> Section { get; } =
                new ObjectStore<SbSection>();
            public ObjectStore<SbSymbol> Symbol { get; } =
                new ObjectStore<SbSymbol>();
        }

        private class LldbMockFactories
        {
            public ILldbFileSpecFactory FileSpec { get; } = Substitute.For<ILldbFileSpecFactory>();
        }

        private class ServerInfo
        {
            public PipeServiceBinder Server { get; }
            public PipeCallInvoker CallInvoker { get; }
            public GrpcConnection Connection { get; }
            public RemoteObjectStores Stores { get; }
            public LldbMockFactories MockFactories { get; }

            public ServerInfo(PipeServiceBinder server, PipeCallInvoker callInvoker,
                GrpcConnection connection, RemoteObjectStores stores,
                LldbMockFactories mockFactories)
            {
                Server = server;
                CallInvoker = callInvoker;
                Connection = connection;
                Stores = stores;
                MockFactories = mockFactories;
            }
        }

        private ServerInfo CreateServer(
            PipeCallInvokerFactory callInvokerFactory, GrpcConnectionFactory connectionFactory)
        {
            PipeCallInvoker callInvoker = callInvokerFactory.Create();
            GrpcConnection connection = connectionFactory.Create(callInvoker);

            string[] inPipeHandles, outPipeHandles;
            callInvoker.GetClientPipeHandles(out inPipeHandles, out outPipeHandles);

            // Note: The client's out handles are the server's in handles and vice versa.
            PipeServiceBinder server = new PipeServiceBinder(outPipeHandles, inPipeHandles);
            var stores = new RemoteObjectStores();
            var mockFactories = new LldbMockFactories();
            BindServices(server, stores, mockFactories);
            server.Start();
            return new ServerInfo(server, callInvoker, connection, stores, mockFactories);
        }

        // This method should grow as we find necessary to interact with other services.
        private void BindServices(ServiceBinderBase server, RemoteObjectStores stores,
            LldbMockFactories mockFactories)
        {
            var remoteFrameRpc =
                new RemoteFrameRpcServiceImpl(stores.Value, stores.Function, stores.Symbol,
                                              stores.Module, stores.Frame, stores.Thread);
            var remoteModuleRpc =
                new SbModuleRpcServiceImpl(stores.Module, stores.Section, mockFactories.FileSpec);
            var remoteThreadRpc = new RemoteThreadRpcServiceImpl(
                stores.Process, stores.Thread,
                stores.Frame, stores.Module);

            RemoteFrameRpcService.BindService(server, remoteFrameRpc);
            SbModuleRpcService.BindService(server, remoteModuleRpc);
            RemoteThreadRpcService.BindService(server, remoteThreadRpc);
        }

        #endregion
    }
}

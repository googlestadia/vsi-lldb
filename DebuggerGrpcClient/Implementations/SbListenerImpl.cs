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

using Debugger.Common;
using Debugger.SbListenerRpc;
using DebuggerApi;

namespace DebuggerGrpcClient
{
    // Creates SBListener objects.
    public class GrpcListenerFactory
    {
        public virtual SbListener Create(GrpcConnection connection, string name)
        {
            var instance = new SbListenerImpl(connection);
            if (instance.Create(name))
            {
                return instance;
            }
            return null;
        }
    }

    // Implementation of the SBDebugger interface that uses GRPC to make RPCs to a remote endpoint.
    class SbListenerImpl : SbListener
    {
        readonly GrpcConnection connection;
        readonly SbListenerRpcService.SbListenerRpcServiceClient client;
        readonly GrpcEventFactory eventFactory;
        GrpcSbListener grpcListener;

        internal SbListenerImpl(GrpcConnection connection)
            : this(connection,
                  new SbListenerRpcService.SbListenerRpcServiceClient(connection.CallInvoker),
                  new GrpcEventFactory()) {}

        // Constructor that can be used by tests to pass in mock objects.
        internal SbListenerImpl(
            GrpcConnection connection,
            SbListenerRpcService.SbListenerRpcServiceClient client,
            GrpcEventFactory eventFactory)
        {
            this.connection = connection;
            this.client = client;
            this.eventFactory = eventFactory;
        }

        internal bool Create(string name)
        {
            CreateResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.Create(new CreateRequest() { Name = name });
                }))
            {
                grpcListener = response.Listener;
                return true;
            }
            return false;
        }

        public bool WaitForEvent(uint numSeconds, out SbEvent evnt)
        {
            WaitForEventResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.WaitForEvent(
                        new WaitForEventRequest
                        {
                            Listener = grpcListener,
                            NumSeconds = numSeconds,
                        });
                }))
            {
                if (response.Result)
                {
                    evnt = eventFactory.Create(response.Event);
                    return true;
                }
            }
            evnt = null;
            return false;
        }

        public long GetId()
        {
            return grpcListener.Id;
        }
    }
}

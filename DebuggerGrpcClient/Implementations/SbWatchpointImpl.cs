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

using System;
using Debugger.Common;
using Debugger.SbWatchpointRpc;
using DebuggerApi;

namespace DebuggerGrpcClient
{
    // Creates SbWatchpoint objects.
    public class GrpcWatchpointFactory
    {
        public virtual SbWatchpoint Create(GrpcConnection connection,
            GrpcSbWatchpoint grpcSbWatchpoint)
        {
            return new SbWatchpointImpl(connection, grpcSbWatchpoint);
        }
    }

    // Implementation of the SBWatchpoint interface that uses GRPC to make RPCs to a remote
    // endpoint.
    public class SbWatchpointImpl : SbWatchpoint
    {
        readonly GrpcConnection connection;
        readonly SbWatchpointRpcService.SbWatchpointRpcServiceClient client;
        readonly GrpcSbWatchpoint grpcSbWatchpoint;

        internal SbWatchpointImpl(
            GrpcConnection connection, GrpcSbWatchpoint grpcSbWatchpoint) :
            this(connection,
                new SbWatchpointRpcService.SbWatchpointRpcServiceClient(connection.CallInvoker),
                grpcSbWatchpoint)
        { }

        internal SbWatchpointImpl(
            GrpcConnection connection, SbWatchpointRpcService.SbWatchpointRpcServiceClient client,
            GrpcSbWatchpoint grpcSbWatchpoint)
        {
            this.connection = connection;
            this.client = client;
            this.grpcSbWatchpoint = grpcSbWatchpoint;
        }

        public int GetId()
        {
            GetIdResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                response = client.GetId(
                    new GetIdRequest { Watchpoint = grpcSbWatchpoint });
                }))
            {
                return response.Id;
            }
            return 0;
        }

        public uint GetHitCount()
        {
            GetHitCountResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.GetHitCount(
                    new GetHitCountRequest { Watchpoint = grpcSbWatchpoint });
            }))
            {
                return response.Result;
            }
            return 0;
        }

        public void SetEnabled(bool enabled)
        {
            SetEnabledResponse response = null;
            connection.InvokeRpc(() =>
            {
                response = client.SetEnabled(
                    new SetEnabledRequest { Watchpoint = grpcSbWatchpoint, Enabled = enabled });
            });
        }

        public void SetCondition(string condition)
        {
            connection.InvokeRpc(() =>
            {
                client.SetCondition(
                    new SetConditionRequest
                    {
                        Watchpoint = grpcSbWatchpoint,
                        Condition = condition
                    });
            });
        }

        public void SetIgnoreCount(uint ignoreCount)
        {
            connection.InvokeRpc(() =>
            {
                client.SetIgnoreCount(
                    new SetIgnoreCountRequest
                    {
                        Watchpoint = grpcSbWatchpoint,
                        IgnoreCount = ignoreCount
                    });
            });
        }
    }
}
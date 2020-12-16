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

using Debugger.RemoteBreakpointRpc;
using Debugger.Common;
using DebuggerApi;
using System;
using System.Collections.Generic;
using RemoteBreakpointRpcServiceClient =
    Debugger.RemoteBreakpointRpc.RemoteBreakpointRpcService.RemoteBreakpointRpcServiceClient;

namespace DebuggerGrpcClient
{
    // <summary>
    // Creates RemoteBreakpoint objects.
    // </summary>
    public class GrpcBreakpointFactory
    {
        public virtual RemoteBreakpoint Create(
            GrpcConnection connection, GrpcSbBreakpoint grpcSbBreakpoint)
        {
            return new RemoteBreakpointProxy(connection, grpcSbBreakpoint);
        }
    }

    // <summary>
    // Implementation of the RemoteBreakpoint interface that uses GRPC to make RPCs to a remote
    // endpoint.
    // </summary>
    class RemoteBreakpointProxy : RemoteBreakpoint
    {
        readonly GrpcConnection connection;
        readonly RemoteBreakpointRpcServiceClient client;
        readonly GrpcSbBreakpoint grpcSbBreakpoint;
        readonly GrpcBreakpointLocationFactory breakpointLocationFactory;

        internal RemoteBreakpointProxy(GrpcConnection connection, GrpcSbBreakpoint grpcSbBreakpoint)
            : this(connection,
                  new RemoteBreakpointRpcServiceClient(connection.CallInvoker),
                  grpcSbBreakpoint, new GrpcBreakpointLocationFactory())
        { }

        internal RemoteBreakpointProxy(
            GrpcConnection connection, RemoteBreakpointRpcServiceClient client,
            GrpcSbBreakpoint grpcSbBreakpoint,
            GrpcBreakpointLocationFactory breakpointLocationFactory)
        {
            this.connection = connection;
            this.client = client;
            this.grpcSbBreakpoint = grpcSbBreakpoint;
            this.breakpointLocationFactory = breakpointLocationFactory;
        }

        public int GetId()
        {
            return grpcSbBreakpoint.Id;
        }

        public void Delete()
        {
            DeleteResponse response = null;
            connection.InvokeRpc(() =>
            {
                response = client.Delete(new DeleteRequest { Breakpoint = grpcSbBreakpoint });
            });
        }

        public uint GetHitCount()
        {
            GetHitCountResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.GetHitCount(
                    new GetHitCountRequest { Breakpoint = grpcSbBreakpoint });
            }))
            {
                return response.Result;
            }
            return 0;
        }

        public SbBreakpointLocation GetLocationAtIndex(uint index)
        {
            GetLocationAtIndexResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.GetLocationAtIndex(
                    new GetLocationAtIndexRequest { Breakpoint = grpcSbBreakpoint, Index = index});
            }))
            {
                if (response.Result != null && response.Result.Id != 0)
                {
                    return breakpointLocationFactory.Create(connection, response.Result);
                }
            }
            return null;
        }

        public uint GetNumLocations()
        {
            GetNumLocationsResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.GetNumLocations(
                    new GetNumLocationsRequest { Breakpoint = grpcSbBreakpoint });
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
                    new SetEnabledRequest { Breakpoint = grpcSbBreakpoint, Enabled = enabled });
            });
        }

        public SbBreakpointLocation FindLocationById(int id)
        {
            throw new NotImplementedException();
        }

        public void SetIgnoreCount(uint ignoreCount)
        {
            connection.InvokeRpc(() =>
            {
                client.SetIgnoreCount(new SetIgnoreCountRequest()
                {
                    Breakpoint = grpcSbBreakpoint,
                    IgnoreCount = ignoreCount
                });
            });
        }

        public void SetOneShot(bool isOneShot)
        {
            connection.InvokeRpc(() =>
            {
                client.SetOneShot(
                    new SetOneShotRequest()
                    {
                        Breakpoint = grpcSbBreakpoint,
                        IsOneShot = isOneShot
                    });
            });
        }

        public void SetCondition(string condition)
        {
            connection.InvokeRpc(() =>
            {
                client.SetCondition(
                    new SetConditionRequest
                    {
                        Breakpoint = grpcSbBreakpoint,
                        Condition = condition
                    });
            });
        }

        public void SetCommandLineCommands(IEnumerable<string> commands)
        {
            var request = new SetCommandLineCommandsRequest
            {
                Breakpoint = grpcSbBreakpoint
            };
            request.Commands.AddRange(commands);
            connection.InvokeRpc(() =>
            {
                client.SetCommandLineCommands(request);
            });
        }
    }
}

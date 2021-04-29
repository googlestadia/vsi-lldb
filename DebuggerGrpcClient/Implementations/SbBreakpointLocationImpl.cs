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

using Debugger.SbBreakpointLocationRpc;
using Debugger.Common;
using DebuggerApi;

namespace DebuggerGrpcClient
{
    // Creates SbBreakpointLocation objects.
    public class GrpcBreakpointLocationFactory
    {
        public virtual SbBreakpointLocation Create(
            GrpcConnection connection, GrpcSbBreakpointLocation grpcSbBreakpointLocation)
        {
            return new SbBreakpointLocationImpl(connection, grpcSbBreakpointLocation);
        }
    }

    // Implementation of the SBBreakpointLocation interface that uses GRPC to make RPCs to a remote
    // endpoint.
    class SbBreakpointLocationImpl : SbBreakpointLocation
    {
        readonly GrpcConnection connection;
        readonly SbBreakpointLocationRpcService.SbBreakpointLocationRpcServiceClient client;
        readonly GrpcSbBreakpointLocation grpcSbBreakpointLocation;
        readonly GrpcBreakpointFactory breakpointFactory;
        readonly GrpcAddressFactory addressFactory;

        internal SbBreakpointLocationImpl(
            GrpcConnection connection, GrpcSbBreakpointLocation grpcSbBreakpointLocation)
            : this(connection,
                  new SbBreakpointLocationRpcService.SbBreakpointLocationRpcServiceClient(
                      connection.CallInvoker),
                  grpcSbBreakpointLocation, new GrpcBreakpointFactory(), new GrpcAddressFactory())
        { }

        internal SbBreakpointLocationImpl(
            GrpcConnection connection,
            SbBreakpointLocationRpcService.SbBreakpointLocationRpcServiceClient client,
            GrpcSbBreakpointLocation grpcSbBreakpointLocation,
            GrpcBreakpointFactory breakpointFactory, GrpcAddressFactory addressFactory)
        {
            this.connection = connection;
            this.client = client;
            this.grpcSbBreakpointLocation = grpcSbBreakpointLocation;
            this.breakpointFactory = breakpointFactory;
            this.addressFactory = addressFactory;
        }

        public int GetId()
        {
            return grpcSbBreakpointLocation.Id;
        }

        public SbAddress GetAddress()
        {
            GetAddressResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetAddress(
                        new GetAddressRequest { BreakpointLocation = grpcSbBreakpointLocation });
                }))
            {
                if (response.Address != null && response.Address.Id != 0)
                {
                    return addressFactory.Create(connection, response.Address);
                }
            }
            return null;
        }

        public ulong GetLoadAddress()
        {
            GetLoadAddressResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.GetLoadAddress(
                    new GetLoadAddressRequest { BreakpointLocation = grpcSbBreakpointLocation });
            }))
            {
                return response.LoadAddress;
            }
            return 0;
        }

        public void SetCondition(string condition)
        {
            connection.InvokeRpc(() =>
            {
                client.SetCondition(
                    new SetConditionRequest()
                    {
                        BreakpointLocation = grpcSbBreakpointLocation,
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
                        BreakpointLocation = grpcSbBreakpointLocation,
                        IgnoreCount = ignoreCount
                    });
            });
        }

        public RemoteBreakpoint GetBreakpoint()
        {
            GetBreakpointResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.GetBreakpoint(
                    new GetBreakpointRequest { BreakpointLocation = grpcSbBreakpointLocation });
            }))
            {
                if (response.Result != null && response.Result.Id != 0)
                {
                    return breakpointFactory.Create(connection, response.Result);
                }
            }
            return null;
        }

        public void SetEnabled(bool enabled)
        {
            SetEnabledResponse response = null;
            connection.InvokeRpc(() =>
            {
                response = client.SetEnabled(
                    new SetEnabledRequest
                    {
                        BreakpointLocation = grpcSbBreakpointLocation,
                        Enabled = enabled
                    });
            });
        }

        public uint GetHitCount()
        {
            GetHitCountResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.GetHitCount(
                    new GetHitCountRequest { BreakpointLocation = grpcSbBreakpointLocation });
            }))
            {
                return response.Result;
            }
            return 0;
        }
    }
}

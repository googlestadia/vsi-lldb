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
using Grpc.Core;
using LldbApi;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace DebuggerGrpcServer
{
    // Server implementation of SbBreakpointLocation RPC.
    class SbBreakpointLocationRpcServiceImpl
        : SbBreakpointLocationRpcService.SbBreakpointLocationRpcServiceBase
    {
        readonly ConcurrentDictionary<long, RemoteTarget> targetStore;
        readonly ObjectStore<SbAddress> addressStore;

        public SbBreakpointLocationRpcServiceImpl(
            ConcurrentDictionary<long, RemoteTarget> targetStore,
            ObjectStore<SbAddress> addressStore)
        {
            this.targetStore = targetStore;
            this.addressStore = addressStore;
        }

        #region SbBreakpointLocationRpcService.SbBreakpointLocationRpcServiceBase

        public override Task<SetEnabledResponse> SetEnabled(SetEnabledRequest request,
            ServerCallContext context)
        {
            var location = GetBreakpointLocation(request.BreakpointLocation);
            location.SetEnabled(request.Enabled);
            return Task.FromResult(new SetEnabledResponse { });
        }

        public override Task<GetBreakpointResponse> GetBreakpoint(
            GetBreakpointRequest request, ServerCallContext context)
        {
            var location = GetBreakpointLocation(request.BreakpointLocation);
            var response = new GetBreakpointResponse();
            var breakpoint = location.GetBreakpoint();
            if (breakpoint != null)
            {
                response.Result = new GrpcSbBreakpoint
                {
                    Target = request.BreakpointLocation.Breakpoint.Target,
                    Id = breakpoint.GetId(),
                };
            }
            return Task.FromResult(response);
        }

        public override Task<GetAddressResponse> GetAddress(
            GetAddressRequest request, ServerCallContext context)
        {
            var location = GetBreakpointLocation(request.BreakpointLocation);
            var response = new GetAddressResponse();
            var address = location.GetAddress();
            if (address != null)
            {
                response.Address = new GrpcSbAddress
                {
                    Id = addressStore.AddObject(address)
                };
            }
            return Task.FromResult(response);
        }

        public override Task<GetLoadAddressResponse> GetLoadAddress(
            GetLoadAddressRequest request, ServerCallContext context)
        {
            var location = GetBreakpointLocation(request.BreakpointLocation);
            return Task.FromResult(
                new GetLoadAddressResponse { LoadAddress = location.GetLoadAddress() });
        }

        public override Task<SetConditionResponse> SetCondition(
            SetConditionRequest request, ServerCallContext context)
        {
            var location = GetBreakpointLocation(request.BreakpointLocation);
            location.SetCondition(request.Condition);
            return Task.FromResult(new SetConditionResponse());
        }

        public override Task<SetIgnoreCountResponse> SetIgnoreCount(
            SetIgnoreCountRequest request, ServerCallContext context)
        {
            var location = GetBreakpointLocation(request.BreakpointLocation);
            location.SetIgnoreCount(request.IgnoreCount);
            return Task.FromResult(new SetIgnoreCountResponse());
        }

        public override Task<GetHitCountResponse> GetHitCount(
            GetHitCountRequest request, ServerCallContext context)
        {
            SbBreakpointLocation location = GetBreakpointLocation(request.BreakpointLocation);
            uint result = location.GetHitCount();
            return Task.FromResult(new GetHitCountResponse { Result = result });
        }

        #endregion

        private SbBreakpointLocation GetBreakpointLocation(
            GrpcSbBreakpointLocation grpcBreakpointLocation)
        {
            var breakpoint = RemoteBreakpointRpcServiceImpl.GetBreakpoint(
                targetStore, grpcBreakpointLocation.Breakpoint);
            var breakpointLocation = breakpoint.FindLocationById(grpcBreakpointLocation.Id);
            if (breakpointLocation == null)
            {
                ErrorUtils.ThrowError(StatusCode.Internal,
                    "Could not find breakpoint location: " + grpcBreakpointLocation.Id);
            }
            return breakpointLocation;
        }
    }
}

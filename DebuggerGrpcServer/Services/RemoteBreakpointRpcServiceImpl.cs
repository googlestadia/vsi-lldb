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
using Grpc.Core;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace DebuggerGrpcServer
{
    // Server implementation of RemoteBreakpoint RPC.
    class RemoteBreakpointRpcServiceImpl :
        RemoteBreakpointRpcService.RemoteBreakpointRpcServiceBase
    {
        readonly ConcurrentDictionary<long, RemoteTarget> _targetStore;

        public RemoteBreakpointRpcServiceImpl(ConcurrentDictionary<long, RemoteTarget> targetStore)
        {
            _targetStore = targetStore;
        }

        #region RemoteBreakpointRpcService.RemoteBreakpointRpcServiceBase

        public override Task<SetEnabledResponse> SetEnabled(SetEnabledRequest request,
            ServerCallContext context)
        {
            var breakpoint = GetBreakpoint(_targetStore, request.Breakpoint);
            breakpoint.SetEnabled(request.Enabled);
            return Task.FromResult(new SetEnabledResponse { });
        }

        public override Task<GetNumLocationsResponse> GetNumLocations(
            GetNumLocationsRequest request, ServerCallContext context)
        {
            var breakpoint = GetBreakpoint(_targetStore, request.Breakpoint);
            var result = breakpoint.GetNumLocations();
            return Task.FromResult(new GetNumLocationsResponse { Result = result });
        }

        public override Task<GetLocationAtIndexResponse> GetLocationAtIndex(
            GetLocationAtIndexRequest request, ServerCallContext context)
        {
            var breakpoint = GetBreakpoint(_targetStore, request.Breakpoint);
            var location = breakpoint.GetLocationAtIndex(request.Index);
            var response = new GetLocationAtIndexResponse();
            response.Result = new GrpcSbBreakpointLocation
            {
                Id = location.GetId(),
                Breakpoint = request.Breakpoint
            };
            return Task.FromResult(response);
        }

        public override Task<GetHitCountResponse> GetHitCount(
            GetHitCountRequest request, ServerCallContext context)
        {
            var breakpoint = GetBreakpoint(_targetStore, request.Breakpoint);
            var result = breakpoint.GetHitCount();
            return Task.FromResult(new GetHitCountResponse { Result = result });
        }

        public override Task<SetIgnoreCountResponse> SetIgnoreCount(
            SetIgnoreCountRequest request, ServerCallContext context)
        {
            var breakpoint = GetBreakpoint(_targetStore, request.Breakpoint);
            breakpoint.SetIgnoreCount(request.IgnoreCount);
            return Task.FromResult(new SetIgnoreCountResponse { });
        }

        public override Task<SetOneShotResponse> SetOneShot(
            SetOneShotRequest request, ServerCallContext context)
        {
            var breakpoint = GetBreakpoint(_targetStore, request.Breakpoint);
            breakpoint.SetOneShot(request.IsOneShot);
            return Task.FromResult(new SetOneShotResponse { });
        }

        public override Task<SetConditionResponse> SetCondition(
            SetConditionRequest request, ServerCallContext context)
        {
            var breakpoint = GetBreakpoint(_targetStore, request.Breakpoint);
            breakpoint.SetCondition(request.Condition);
            return Task.FromResult(new SetConditionResponse {});
        }

        public override Task<SetCommandLineCommandsResponse> SetCommandLineCommands(
            SetCommandLineCommandsRequest request, ServerCallContext context)
        {
            var breakpoint = GetBreakpoint(_targetStore, request.Breakpoint);
            breakpoint.SetCommandLineCommands(request.Commands.ToList());
            return Task.FromResult(new SetCommandLineCommandsResponse {});
        }

        #endregion

        internal static RemoteBreakpoint GetBreakpoint(
            ConcurrentDictionary<long, RemoteTarget> targetStore,
            GrpcSbBreakpoint grpcSbBreakpoint)
        {
            RemoteTarget target = null;
            if (!targetStore.TryGetValue(grpcSbBreakpoint.Target.Id, out target))
            {
                ErrorUtils.ThrowError(StatusCode.Internal,
                    "Could not find target in store: " + grpcSbBreakpoint.Target.Id);
            }
            var breakpoint = target.FindBreakpointById(grpcSbBreakpoint.Id);
            if (breakpoint == null)
            {
                ErrorUtils.ThrowError(StatusCode.Internal,
                    "Could not find breakpoint in target: " + grpcSbBreakpoint.Id);
            }
            return breakpoint;
        }
    }
}

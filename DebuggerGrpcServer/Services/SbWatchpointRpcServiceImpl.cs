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

using Debugger.SbWatchpointRpc;
using Grpc.Core;
using LldbApi;
using System.Threading.Tasks;

namespace DebuggerGrpcServer
{
    // Server implementation of SbWatchpoint RPC.
    class SbWatchpointRpcServiceImpl : SbWatchpointRpcService.SbWatchpointRpcServiceBase
    {
        readonly ObjectStore<SbWatchpoint> watchpointStore;

        public SbWatchpointRpcServiceImpl(ObjectStore<SbWatchpoint> watchpointStore)
        {
            this.watchpointStore = watchpointStore;
        }

        #region SbBreakpointRpcService.SbBreakpointRpcServiceBase

        public override Task<GetIdResponse> GetId(GetIdRequest request, ServerCallContext context)
        {
            var watchpoint = watchpointStore.GetObject(request.Watchpoint.Id);
            return Task.FromResult(new GetIdResponse
            {
                Id = watchpoint.GetId()
            });
        }

        public override Task<GetHitCountResponse> GetHitCount(
            GetHitCountRequest request, ServerCallContext context)
        {
            var watchpoint = watchpointStore.GetObject(request.Watchpoint.Id);
            var result = watchpoint.GetHitCount();
            return Task.FromResult(new GetHitCountResponse { Result = result });
        }

        public override Task<SetEnabledResponse> SetEnabled(SetEnabledRequest request,
            ServerCallContext context)
        {
            var watchpoint = watchpointStore.GetObject(request.Watchpoint.Id);
            watchpoint.SetEnabled(request.Enabled);
            return Task.FromResult(new SetEnabledResponse { });
        }

        public override Task<SetConditionResponse> SetCondition(
            SetConditionRequest request, ServerCallContext context)
        {
            var watchpoint = watchpointStore.GetObject(request.Watchpoint.Id);
            watchpoint.SetCondition(request.Condition);
            return Task.FromResult(new SetConditionResponse { });
        }

        public override Task<SetIgnoreCountResponse> SetIgnoreCount(
            SetIgnoreCountRequest request, ServerCallContext context)
        {
            var watchpoint = watchpointStore.GetObject(request.Watchpoint.Id);
            watchpoint.SetIgnoreCount(request.IgnoreCount);
            return Task.FromResult(new SetIgnoreCountResponse { });
        }
        #endregion

    }
}

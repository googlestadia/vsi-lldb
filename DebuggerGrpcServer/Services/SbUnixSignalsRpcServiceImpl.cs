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

using Debugger.SbUnixSignalsRpc;
using Grpc.Core;
using LldbApi;
using System.Threading.Tasks;

namespace DebuggerGrpcServer
{
    class SbUnixSignalsRpcServiceImpl : SbUnixSignalsRpcService.SbUnixSignalsRpcServiceBase
    {
        readonly ObjectStore<SbUnixSignals> unixSignalsStore;

        public SbUnixSignalsRpcServiceImpl(ObjectStore<SbUnixSignals> signalsStore)
        {
            this.unixSignalsStore = signalsStore;
        }

        #region SbUnixSignalsRpcService.SbUnixSignalsRpcServiceBase

        public override Task<DeleteResponse> Delete(DeleteRequest request,
            ServerCallContext context)
        {
            unixSignalsStore.RemoveObject(request.Signals.Id);
            return Task.FromResult(new DeleteResponse());
        }

        public override Task<GetShouldStopResponse> GetShouldStop(GetShouldStopRequest request,
            ServerCallContext context)
        {
            var unixSignals = unixSignalsStore.GetObject(request.Signals.Id);
            return Task.FromResult(new GetShouldStopResponse
            {
                ShouldStop = unixSignals.GetShouldStop(request.SignalNumber)
            });
        }

        public override Task<SetShouldStopResponse> SetShouldStop(SetShouldStopRequest request,
            ServerCallContext context)
        {
            var unixSignals = unixSignalsStore.GetObject(request.Signals.Id);
            return Task.FromResult(new SetShouldStopResponse
            {
                Result = unixSignals.SetShouldStop(request.SignalNumber, request.Value)
            });
        }

        #endregion
    }
}

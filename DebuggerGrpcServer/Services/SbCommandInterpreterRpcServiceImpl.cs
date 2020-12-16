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

using Debugger.SbCommandInterpreterRpc;
using Grpc.Core;
using LldbApi;
using System.Threading.Tasks;
using YetiCommon;

namespace DebuggerGrpcServer
{
    class SbCommandInterpreterRpcServiceImpl
        : SbCommandInterpreterRpcService.SbCommandInterpreterRpcServiceBase
    {
        readonly ObjectStore<SbCommandInterpreter> interpreterStore;
        readonly ObjectStore<SbCommandReturnObject> returnObjectStore;

        public SbCommandInterpreterRpcServiceImpl(
            ObjectStore<SbCommandInterpreter> interpreterStore,
            ObjectStore<SbCommandReturnObject> returnObjectStore)
        {
            this.interpreterStore = interpreterStore;
            this.returnObjectStore = returnObjectStore;
        }

        #region SbCommandInterpreterRpcService.SbCommandInterpreterRpcServiceBase

        public override Task<DeleteResponse> Delete(DeleteRequest request,
            ServerCallContext context)
        {
            interpreterStore.RemoveObject(request.Interpreter.Id);
            return Task.FromResult(new DeleteResponse());
        }

        public override Task<HandleCommandResponse> HandleCommand(HandleCommandRequest request,
            ServerCallContext context)
        {
            var interpreter = interpreterStore.GetObject(request.Interpreter.Id);
            var response = new HandleCommandResponse();

            SbCommandReturnObject returnObject;
            response.ReturnStatus = interpreter.HandleCommand(request.Command,
                out returnObject).ConvertTo<Debugger.Common.ReturnStatus>();
            if (returnObject != null)
            {
                response.ReturnObject = new Debugger.Common.GrpcSbCommandReturnObject
                {
                    Id = returnObjectStore.AddObject(returnObject)
                };
            }
            return Task.FromResult(response);
        }

        public override Task<SourceInitFileInHomeDirectoryResponse>
            SourceInitFileInHomeDirectory(SourceInitFileInHomeDirectoryRequest request,
                ServerCallContext context)
        {
            var interpreter = interpreterStore.GetObject(request.Interpreter.Id);
            interpreter.SourceInitFileInHomeDirectory();
            return Task.FromResult(new SourceInitFileInHomeDirectoryResponse());
        }

        #endregion
    }
}

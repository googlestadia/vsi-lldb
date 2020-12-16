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
using Debugger.RemoteTargetRpc;
using DebuggerCommonApi;
using DebuggerGrpcServer.RemoteInterfaces;
using Grpc.Core;
using LldbApi;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DebuggerGrpcServer
{
    // Server implementation of the RemoteTarget RPC.
    class RemoteTargetRpcServiceImpl : RemoteTargetRpcService.RemoteTargetRpcServiceBase
    {
        readonly ConcurrentDictionary<long, RemoteTarget> _targetStore;
        readonly ConcurrentDictionary<long, SbListener> _listenerStore;
        readonly ConcurrentDictionary<int, SbProcess> _processStore;
        readonly UniqueObjectStore<SbModule> _moduleStore;
        readonly ObjectStore<SbWatchpoint> _watchpointStore;
        readonly ObjectStore<SbAddress> _addressStore;

        public RemoteTargetRpcServiceImpl(ConcurrentDictionary<long, RemoteTarget> targetStore,
                                          ConcurrentDictionary<long, SbListener> listenerStore,
                                          ConcurrentDictionary<int, SbProcess> processStore,
                                          UniqueObjectStore<SbModule> moduleStore,
                                          ObjectStore<SbWatchpoint> watchpointStore,
                                          ObjectStore<SbAddress> addressStore)
        {
            _targetStore = targetStore;
            _listenerStore = listenerStore;
            _processStore = processStore;
            _moduleStore = moduleStore;
            _watchpointStore = watchpointStore;
            _addressStore = addressStore;
        }

#region RemoteTargetRpcService.RemoteTargetRpcServiceBase

        public override Task<AttachToProcessWithIDResponse> AttachToProcessWithID(
            AttachToProcessWithIDRequest request, ServerCallContext context)
        {
            if (!_targetStore.TryGetValue(request.Target.Id, out RemoteTarget target))
            {
                ErrorUtils.ThrowError(StatusCode.Internal,
                                      "Could not find target in store: " + request.Target.Id);
            }

            if (!_listenerStore.TryGetValue(request.Listener.Id, out SbListener listener))
            {
                ErrorUtils.ThrowError(StatusCode.Internal,
                                      "Could not find listener in store: " + request.Listener.Id);
            }

            SbProcess process =
                target.AttachToProcessWithID(listener, request.Pid, out SbError error);

            var response =
                new AttachToProcessWithIDResponse { Error = new GrpcSbError {
                    Success = error.Success(),
                    Error = error.GetCString(),
                } };
            if (process != null)
            {
                if (!_processStore.TryAdd(process.GetUniqueId(), process))
                {
                    ErrorUtils.ThrowError(StatusCode.Internal, "Could not add process to store: " +
                                                                   process.GetUniqueId());
                }
                response.Process = new GrpcSbProcess { Id = process.GetUniqueId() };
            }
            return Task.FromResult(response);
        }

        public override Task<BreakpointCreateByLocationResponse> BreakpointCreateByLocation(
            BreakpointCreateByLocationRequest request, ServerCallContext context)
        {
            if (!_targetStore.TryGetValue(request.Target.Id, out RemoteTarget target))
            {
                ErrorUtils.ThrowError(StatusCode.Internal,
                                      "Could not find target in store: " + request.Target.Id);
            }

            var response = new BreakpointCreateByLocationResponse();
            RemoteBreakpoint breakpoint =
                target.BreakpointCreateByLocation(request.File, request.Line);
            if (breakpoint != null)
            {
                response.Breakpoint = new GrpcSbBreakpoint {
                    Target = request.Target,
                    Id = breakpoint.GetId(),
                };
            }
            return Task.FromResult(response);
        }

        public override Task<BreakpointCreateByNameResponse> BreakpointCreateByName(
            BreakpointCreateByNameRequest request, ServerCallContext context)
        {
            if (!_targetStore.TryGetValue(request.Target.Id, out RemoteTarget target))
            {
                ErrorUtils.ThrowError(StatusCode.Internal,
                                      "Could not find target in store: " + request.Target.Id);
            }

            var response = new BreakpointCreateByNameResponse();
            RemoteBreakpoint breakpoint = target.BreakpointCreateByName(request.SymbolName);
            if (breakpoint != null)
            {
                response.Breakpoint = new GrpcSbBreakpoint {
                    Target = request.Target,
                    Id = breakpoint.GetId(),
                };
            }
            return Task.FromResult(response);
        }

        public override Task<BreakpointCreateByAddressResponse> BreakpointCreateByAddress(
            BreakpointCreateByAddressRequest request, ServerCallContext context)
        {
            if (!_targetStore.TryGetValue(request.Target.Id, out RemoteTarget target))
            {
                ErrorUtils.ThrowError(StatusCode.Internal,
                                      "Could not find target in store: " + request.Target.Id);
            }

            var response = new BreakpointCreateByAddressResponse();
            RemoteBreakpoint breakpoint = target.BreakpointCreateByAddress(request.Address);
            if (breakpoint != null)
            {
                response.Breakpoint = new GrpcSbBreakpoint {
                    Target = request.Target,
                    Id = breakpoint.GetId(),
                };
            }
            return Task.FromResult(response);
        }

        public override Task<CreateFunctionOffsetBreakpointResponse> CreateFunctionOffsetBreakpoint(
            CreateFunctionOffsetBreakpointRequest request, ServerCallContext context)
        {
            if (!_targetStore.TryGetValue(request.Target.Id, out RemoteTarget target))
            {
                ErrorUtils.ThrowError(StatusCode.Internal,
                                      "Could not find target in store: " + request.Target.Id);
            }

            var response = new CreateFunctionOffsetBreakpointResponse();
            BreakpointErrorPair breakpointErrorPair =
                target.CreateFunctionOffsetBreakpoint(request.SymbolName, request.Offset);
            if (breakpointErrorPair.breakpoint != null)
            {
                response.Breakpoint = new GrpcSbBreakpoint {
                    Target = request.Target,
                    Id = breakpointErrorPair.breakpoint.GetId(),
                };
            }
            response.Error = (Debugger.Common.BreakpointError)breakpointErrorPair.error;
            return Task.FromResult(response);
        }

        public override Task<BreakpointDeleteResponse> BreakpointDelete(
            BreakpointDeleteRequest request, ServerCallContext context)
        {
            if (!_targetStore.TryGetValue(request.Target.Id, out RemoteTarget target))
            {
                ErrorUtils.ThrowError(StatusCode.Internal,
                                      "Could not find target in store: " + request.Target.Id);
            }
            bool success = target.BreakpointDelete(request.BreakpointId);
            var response = new BreakpointDeleteResponse { Success = success };
            return Task.FromResult(response);
        }

        public override Task<GetNumModulesResponse> GetNumModules(GetNumModulesRequest request,
                                                                  ServerCallContext context)
        {
            if (!_targetStore.TryGetValue(request.Target.Id, out RemoteTarget target))
            {
                ErrorUtils.ThrowError(StatusCode.Internal,
                                      "Could not find target in store: " + request.Target.Id);
            }

            var response = new GetNumModulesResponse();
            response.Result = target.GetNumModules();
            return Task.FromResult(response);
        }

        public override Task<GetModuleAtIndexResponse> GetModuleAtIndex(
            GetModuleAtIndexRequest request, ServerCallContext context)
        {
            if (!_targetStore.TryGetValue(request.Target.Id, out RemoteTarget target))
            {
                ErrorUtils.ThrowError(StatusCode.Internal,
                                      "Could not find target in store: " + request.Target.Id);
            }

            var response = new GetModuleAtIndexResponse();
            SbModule module = target.GetModuleAtIndex(request.Index);
            if (module != null)
            {
                response.Module = new GrpcSbModule { Id = _moduleStore.AddObject(module) };
            }
            return Task.FromResult(response);
        }

        public override Task<WatchAddressResponse> WatchAddress(WatchAddressRequest request,
                                                                ServerCallContext context)
        {
            if (!_targetStore.TryGetValue(request.Target.Id, out RemoteTarget target))
            {
                ErrorUtils.ThrowError(StatusCode.Internal,
                                      "Could not find target in store: " + request.Target.Id);
            }

            var response = new WatchAddressResponse();
            SbWatchpoint watchpoint = target.WatchAddress(
                request.Address, request.Size, request.Read, request.Write, out SbError error);
            response.Error = new GrpcSbError {
                Success = error.Success(),
                Error = error.GetCString(),
            };
            if (watchpoint != null)
            {
                response.Watchpoint = new GrpcSbWatchpoint {
                    Id = _watchpointStore.AddObject(watchpoint),
                };
            }
            return Task.FromResult(response);
        }

        public override Task<DeleteWatchpointResponse> DeleteWatchpoint(
            DeleteWatchpointRequest request, ServerCallContext context)
        {
            if (!_targetStore.TryGetValue(request.Target.Id, out RemoteTarget target))
            {
                ErrorUtils.ThrowError(StatusCode.Internal,
                                      "Could not find target in store: " + request.Target.Id);
            }
            bool success = target.DeleteWatchpoint(request.WatchId);
            var response = new DeleteWatchpointResponse { Success = success };
            return Task.FromResult(response);
        }

        public override Task<ResolveLoadAddressResponse> ResolveLoadAddress(
            ResolveLoadAddressRequest request, ServerCallContext context)
        {
            RemoteTarget target = GrpcLookupUtils.GetTarget(request.Target, _targetStore);
            var response = new ResolveLoadAddressResponse();
            SbAddress address = target.ResolveLoadAddress(request.Address);
            if (address != null)
            {
                response.Address = new GrpcSbAddress {
                    Id = _addressStore.AddObject(address),
                };
            }
            return Task.FromResult(response);
        }

        public override Task<LoadCoreResponse> LoadCore(LoadCoreRequest request,
                                                        ServerCallContext context)
        {
            RemoteTarget target = GrpcLookupUtils.GetTarget(request.Target, _targetStore);
            SbProcess process = target.LoadCore(request.CorePath);
            var response = new LoadCoreResponse();
            if (process != null)
            {
                if (!_processStore.TryAdd(process.GetUniqueId(), process))
                {
                    ErrorUtils.ThrowError(StatusCode.Internal, "Could not add process to store: " +
                                                                   process.GetUniqueId());
                }
                response.Process = new GrpcSbProcess { Id = process.GetUniqueId() };
            }
            return Task.FromResult(response);
        }

        public override Task<AddModuleResponse> AddModule(AddModuleRequest request,
                                                          ServerCallContext context)
        {
            RemoteTarget target = GrpcLookupUtils.GetTarget(request.Target, _targetStore);
            var response = new AddModuleResponse();
            SbModule module = target.AddModule(request.Path, request.Triple, request.Uuid);
            if (module != null)
            {
                response.Module = new GrpcSbModule { Id = _moduleStore.AddObject(module) };
            }
            return Task.FromResult(response);
        }

        public override Task<RemoveModuleResponse> RemoveModule(RemoveModuleRequest request,
                                                                ServerCallContext context)
        {
            RemoteTarget target = GrpcLookupUtils.GetTarget(request.Target, _targetStore);
            SbModule module = _moduleStore.GetObject(request.Module.Id);
            return Task.FromResult(
                new RemoveModuleResponse { Result = target.RemoveModule(module) });
        }

        public override Task<SetModuleLoadAddressResponse> SetModuleLoadAddress(
            SetModuleLoadAddressRequest request, ServerCallContext context)
        {
            RemoteTarget target = GrpcLookupUtils.GetTarget(request.Target, _targetStore);
            SbModule module = _moduleStore.GetObject(request.Module.Id);
            SbError error = target.SetModuleLoadAddress(module, request.SectionsOffset);
            var grpcError =
                new GrpcSbError { Success = error.Success(), Error = error.GetCString() };
            return Task.FromResult(new SetModuleLoadAddressResponse { Error = grpcError });
        }

        public override Task<ReadInstructionInfosResponse> ReadInstructionInfos(
            ReadInstructionInfosRequest request, ServerCallContext context)
        {
            RemoteTarget target = GrpcLookupUtils.GetTarget(request.Target, _targetStore);
            SbAddress address = _addressStore.GetObject(request.Address.Id);
            var response = new ReadInstructionInfosResponse();
            List<InstructionInfo> instructions =
                target.ReadInstructionInfos(address, request.Count, request.Flavor);
            if (instructions != null)
            {
                foreach (InstructionInfo instruction in instructions)
                {
                    var instructionInfo = new GrpcInstructionInfo {
                        Address = instruction.Address, Operands = instruction.Operands ?? "",
                        Comment = instruction.Comment ?? "", Mnemonic = instruction.Mnemonic ?? "",
                        SymbolName = instruction.SymbolName ?? ""
                    };
                    if (instruction.LineEntry != null)
                    {
                        instructionInfo.LineEntry = new GrpcLineEntryInfo {
                            FileName = instruction.LineEntry.FileName ?? "",
                            Directory = instruction.LineEntry.Directory ?? "",
                            Line = instruction.LineEntry.Line, Column = instruction.LineEntry.Column
                        };
                    }
                    response.Instructions.Add(instructionInfo);
                }
            }
            return Task.FromResult(response);
        }

#endregion
    }
}

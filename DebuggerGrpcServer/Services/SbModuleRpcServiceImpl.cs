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
using Debugger.SbModuleRpc;
using Grpc.Core;
using LldbApi;
using System.Threading.Tasks;

namespace DebuggerGrpcServer
{
    // Server implementation of the SBModule RPC.
    public class SbModuleRpcServiceImpl : SbModuleRpcService.SbModuleRpcServiceBase
    {
        readonly UniqueObjectStore<SbModule> moduleStore;
        readonly ObjectStore<SbAddress> addressStore;
        readonly ObjectStore<SbSection> sectionStore;
        readonly ILldbFileSpecFactory fileSpecFactory;

        public SbModuleRpcServiceImpl(UniqueObjectStore<SbModule> moduleStore,
                                      ObjectStore<SbAddress> addressStore,
                                      ObjectStore<SbSection> sectionStore,
                                      ILldbFileSpecFactory fileSpecFactory)
        {
            this.moduleStore = moduleStore;
            this.addressStore = addressStore;
            this.sectionStore = sectionStore;
            this.fileSpecFactory = fileSpecFactory;
        }

        #region SbModuleRpcService.SbModuleRpcServiceBase
        public override Task<BulkDeleteResponse> BulkDelete(BulkDeleteRequest request,
            ServerCallContext context)
        {
            foreach (GrpcSbModule module in request.Modules)
            {
                moduleStore.RemoveObject(module.Id);
            }
            return Task.FromResult(new BulkDeleteResponse());
        }

        public override Task<GetFileSpecResponse> GetFileSpec(
            GetFileSpecRequest request, ServerCallContext context)
        {
            var module = moduleStore.GetObject(request.Module.Id);
            var response = new GetFileSpecResponse();
            var fileSpec = module.GetFileSpec();
            if (fileSpec != null)
            {
                response.FileSpec = new GrpcSbFileSpec
                {
                    Filename = fileSpec.GetFilename(),
                    Directory = fileSpec.GetDirectory(),
                };
            }
            return Task.FromResult(response);
        }

        public override Task<GetPlatformFileSpecResponse> GetPlatformFileSpec(
            GetPlatformFileSpecRequest request, ServerCallContext context)
        {
            var module = moduleStore.GetObject(request.Module.Id);
            var response = new GetPlatformFileSpecResponse();
            var fileSpec = module.GetPlatformFileSpec();
            if (fileSpec != null)
            {
                response.FileSpec = new GrpcSbFileSpec
                {
                    Filename = fileSpec.GetFilename(),
                    Directory = fileSpec.GetDirectory(),
                };
            }
            return Task.FromResult(response);
        }

        public override Task<SetPlatformFileSpecResponse> SetPlatformFileSpec(
            SetPlatformFileSpecRequest request, ServerCallContext context)
        {
            var module = moduleStore.GetObject(request.Module.Id);
            SbFileSpec fileSpec = fileSpecFactory.Create(request.FileSpec.Directory,
                request.FileSpec.Filename);
            return Task.FromResult(
                new SetPlatformFileSpecResponse { Result = module.SetPlatformFileSpec(fileSpec) });
        }

        public override Task<GetSymbolFileSpecResponse> GetSymbolFileSpec(
            GetSymbolFileSpecRequest request, ServerCallContext context)
        {
            var module = moduleStore.GetObject(request.Module.Id);
            var response = new GetSymbolFileSpecResponse();
            var fileSpec = module.GetSymbolFileSpec();
            if (fileSpec != null)
            {
                response.FileSpec = new GrpcSbFileSpec
                {
                    Filename = fileSpec.GetFilename(),
                    Directory = fileSpec.GetDirectory(),
                };
            }
            return Task.FromResult(response);
        }

        public override Task<GetCodeLoadAddressResponse> GetCodeLoadAddress(
            GetCodeLoadAddressRequest request, ServerCallContext context)
        {
            var module = moduleStore.GetObject(request.Module.Id);
            return Task.FromResult(
                new GetCodeLoadAddressResponse { CodeLoadAddress = module.GetCodeLoadAddress() });
        }

        public override Task<GetObjectFileHeaderAddressResponse> GetObjectFileHeaderAddress(
            GetObjectFileHeaderAddressRequest request, ServerCallContext context)
        {
            var module = moduleStore.GetObject(request.Module.Id);
            SbAddress address = module.GetObjectFileHeaderAddress();
            var response = new GetObjectFileHeaderAddressResponse();
            if (address != null)
            {
                response.Address = new GrpcSbAddress { Id = addressStore.AddObject(address) };
            }
            return Task.FromResult(response);
        }

        public override Task<GetCodeSizeResponse> GetCodeSize(
            GetCodeSizeRequest request, ServerCallContext context)
        {
            var module = moduleStore.GetObject(request.Module.Id);
            return Task.FromResult(new GetCodeSizeResponse { CodeSize = module.GetCodeSize() });
        }

        public override Task<Is64BitResponse> Is64Bit(
            Is64BitRequest request, ServerCallContext context)
        {
            var module = moduleStore.GetObject(request.Module.Id);
            return Task.FromResult(new Is64BitResponse { Result = module.Is64Bit() });
        }

        public override Task<HasSymbolsResponse> HasSymbols(
            HasSymbolsRequest request, ServerCallContext context)
        {
            var module = moduleStore.GetObject(request.Module.Id);
            return Task.FromResult(new HasSymbolsResponse { Result = module.HasSymbols() });
        }

        public override Task<HasCompileUnitsResponse> HasCompileUnits(
            HasCompileUnitsRequest request, ServerCallContext context)
        {
            var module = moduleStore.GetObject(request.Module.Id);
            return Task.FromResult(
                new HasCompileUnitsResponse { Result = module.HasCompileUnits() });
        }

        public override Task<GetNumCompileUnitsResponse> GetNumCompileUnits(
            GetNumCompileUnitsRequest request, ServerCallContext context)
        {
            var module = moduleStore.GetObject(request.Module.Id);
            return Task.FromResult(
                new GetNumCompileUnitsResponse
                {
                    NumCompileUnits = module.GetNumCompileUnits(),
                });
        }

        public override Task<GetUUIDStringResponse> GetUUIDString(GetUUIDStringRequest request,
            ServerCallContext context)
        {
            var module = moduleStore.GetObject(request.Module.Id);
            return Task.FromResult(new GetUUIDStringResponse { Uuid = module.GetUUIDString() });
        }

        public override Task<GetTripleResponse> GetTriple(GetTripleRequest request,
            ServerCallContext context)
        {
            var module = moduleStore.GetObject(request.Module.Id);
            return Task.FromResult(new GetTripleResponse { Triple = module.GetTriple() });
        }

        public override Task<FindSectionResponse> FindSection(FindSectionRequest request,
                                                              ServerCallContext context)
        {
            var module = moduleStore.GetObject(request.Module.Id);
            var section = module.FindSection(request.Name);
            var response = new FindSectionResponse();
            if (section != null)
            {
                response.Section = new GrpcSbSection { Id = sectionStore.AddObject(section) };
            }
            return Task.FromResult(response);
        }

        public override Task<GetNumSectionsResponse> GetNumSections(GetNumSectionsRequest request,
            ServerCallContext context)
        {
            var module = moduleStore.GetObject(request.Module.Id);
            return Task.FromResult(
                new GetNumSectionsResponse { NumSections = module.GetNumSections() });
        }

        public override Task<GetSectionAtIndexResponse> GetSectionAtIndex(
            GetSectionAtIndexRequest request, ServerCallContext context)
        {
            var module = moduleStore.GetObject(request.Module.Id);
            var section = module.GetSectionAtIndex(request.Index);
            var response = new GetSectionAtIndexResponse();
            if (section != null)
            {
                response.Section = new GrpcSbSection { Id = sectionStore.AddObject(section) };
            }
            return Task.FromResult(response);
        }

        #endregion
    }
}

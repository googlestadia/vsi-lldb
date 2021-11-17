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
using DebuggerApi;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SbModuleRpcServiceClient = Debugger.SbModuleRpc.SbModuleRpcService.SbModuleRpcServiceClient;


namespace DebuggerGrpcClient
{
    // Creates SbModule objects.
    public class GrpcModuleFactory
    {
        public virtual SbModule Create(GrpcConnection connection, GrpcSbModule grpcSbModule)
        {
            return new SbModuleImpl(connection, grpcSbModule);
        }
    }

    // Implementation of the SbModule interface that uses GRPC to make RPCs to a remote
    // endpoint.
    public class SbModuleImpl : SbModule, IDisposable
    {
        readonly GrpcConnection connection;
        readonly GrpcSbModule grpcSbModule;
        readonly SbModuleRpcServiceClient client;
        readonly GrpcAddressFactory addressFactory;
        readonly GrpcFileSpecFactory fileSpecFactory;
        readonly GrpcSectionFactory sectionFactory;
        readonly GCHandle gcHandle;

        private bool disposed = false;

        internal SbModuleImpl(GrpcConnection connection, GrpcSbModule grpcSbModule)
            : this(connection, grpcSbModule, new SbModuleRpcServiceClient(connection.CallInvoker),
                   new GrpcAddressFactory(), new GrpcFileSpecFactory(), new GrpcSectionFactory())
        {
        }

        internal SbModuleImpl(GrpcConnection connection, GrpcSbModule grpcSbModule,
                              SbModuleRpcServiceClient client, GrpcAddressFactory addressFactory,
                              GrpcFileSpecFactory fileSpecFactory,
                              GrpcSectionFactory sectionFactory)
        {
            this.connection = connection;
            this.grpcSbModule = grpcSbModule;
            this.client = client;
            this.addressFactory = addressFactory;
            this.fileSpecFactory = fileSpecFactory;
            this.sectionFactory = sectionFactory;

            // Keep a handle to objects we need in the destructor.
            gcHandle = GCHandle.Alloc(
                new Tuple<GrpcConnection, SbModuleRpcServiceClient, GrpcSbModule>(
                    connection, client, grpcSbModule));
        }

        ~SbModuleImpl()
        {
            Dispose(false);
        }

        // Disposing indicates whether this method has been called by user's code or by the
        // the destructor. In the latter case, we should not reference managed objects as we cannot
        // know if they have already been reclaimed by the garbage collector.
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                // We only have unmanaged resources to dispose of in this class.
                connection
                    .GetOrCreateBulkDeleter<GrpcSbModule>()
                    .QueueForDeletion(grpcSbModule, (List<GrpcSbModule> modules) =>
                    {
                        var request = new BulkDeleteRequest();
                        request.Modules.AddRange(modules);
                        connection.InvokeRpc(() =>
                        {
                            client.BulkDelete(request);
                        });
                    });
                gcHandle.Free();
                disposed = true;
            }
        }

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            // Prevent finalization code for this object from executing a second time.
            GC.SuppressFinalize(this);
        }

        #endregion

        #region SbModule

        public ulong GetCodeLoadAddress()
        {
            GetCodeLoadAddressResponse response = null;
            if (connection.InvokeRpc(() => {
                    response = client.GetCodeLoadAddress(
                        new GetCodeLoadAddressRequest() { Module = grpcSbModule });
                }))
            {
                return response.CodeLoadAddress;
            }
            return 0;
        }

        public SbAddress GetObjectFileHeaderAddress()
        {
            GetObjectFileHeaderAddressResponse response = null;
            if (connection.InvokeRpc(() => {
                    response = client.GetObjectFileHeaderAddress(
                        new GetObjectFileHeaderAddressRequest() { Module = grpcSbModule });
                }))
            {
                return addressFactory.Create(connection, response.Address);
            }
            return null;
        }

        public ulong GetCodeSize()
        {
            GetCodeSizeResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetCodeSize(
                        new GetCodeSizeRequest() { Module = grpcSbModule });
                }))
            {
                return response.CodeSize;
            }
            return 0;
        }

        public SbFileSpec GetFileSpec()
        {
            GetFileSpecResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetFileSpec(
                        new GetFileSpecRequest() { Module = grpcSbModule });
                }))
            {
                if (response.FileSpec != null)
                {
                    return fileSpecFactory.Create(response.FileSpec);
                }
            }
            return null;
        }

        public SbFileSpec GetPlatformFileSpec()
        {
            GetPlatformFileSpecResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetPlatformFileSpec(
                        new GetPlatformFileSpecRequest() { Module = grpcSbModule });
                }))
            {
                if (response.FileSpec != null)
                {
                    return fileSpecFactory.Create(response.FileSpec);
                }
            }
            return null;
        }

        public bool SetPlatformFileSpec(string fileDirectory, string fileName)
        {
            SetPlatformFileSpecResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.SetPlatformFileSpec(
                    new SetPlatformFileSpecRequest
                    {
                        Module = grpcSbModule,
                        FileSpec = new GrpcSbFileSpec
                        {
                            Directory = fileDirectory,
                            Filename = fileName,
                        },
                    });
            }))
            {
                return response.Result;
            }
            return false;
        }

        public bool SetPlatformFileSpec(SbFileSpec fileSpec)
        {
            return SetPlatformFileSpec(fileSpec.GetDirectory(), fileSpec.GetFilename());
        }

        public SbFileSpec GetSymbolFileSpec()
        {
            GetSymbolFileSpecResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.GetSymbolFileSpec(
                    new GetSymbolFileSpecRequest() { Module = grpcSbModule });
            }))
            {
                if (response.FileSpec != null)
                {
                    return fileSpecFactory.Create(response.FileSpec);
                }
            }
            return null;
        }

        public bool HasCompileUnits()
        {
            HasCompileUnitsResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.HasCompileUnits(
                        new HasCompileUnitsRequest() { Module = grpcSbModule });
                }))
            {
                return response.Result;
            }
            return false;
        }

        public bool HasSymbols()
        {
            HasSymbolsResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.HasSymbols(
                        new HasSymbolsRequest() { Module = grpcSbModule });
                }))
            {
                return response.Result;
            }
            return false;
        }

        public bool Is64Bit()
        {
            Is64BitResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.Is64Bit(
                        new Is64BitRequest { Module = grpcSbModule });
                }))
            {
                return response.Result;
            }
            return false;
        }

        public uint GetNumCompileUnits()
        {
            GetNumCompileUnitsResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetNumCompileUnits(
                        new GetNumCompileUnitsRequest
                        {
                            Module = grpcSbModule
                        });
                }))
            {
                return response.NumCompileUnits;
            }
            return 0;
        }

        public string GetUUIDString()
        {
            GetUUIDStringResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetUUIDString(
                        new GetUUIDStringRequest { Module = grpcSbModule });
                }))
            {
                return response.Uuid;
            }
            return "";
        }

        public string GetTriple()
        {
            GetTripleResponse response = null;
            return connection.InvokeRpc(() =>
            {
                response = client.GetTriple(
                    new GetTripleRequest { Module = grpcSbModule });
            })
                ? response.Triple
                : "";
        }

        public SbSection FindSection(string name)
        {
            FindSectionResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.FindSection(
                        new FindSectionRequest
                        {
                            Module = grpcSbModule,
                            Name = name,
                        });
                }))
            {
                if (response.Section != null && response.Section.Id != 0)
                {
                    return sectionFactory.Create(connection, response.Section);
                }
            }
            return null;
        }

        public ulong GetNumSections()
        {
            GetNumSectionsResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetNumSections(
                        new GetNumSectionsRequest
                        {
                            Module = grpcSbModule
                        });
                }))
            {
                return response.NumSections;
            }
            return 0;
        }

        public SbSection GetSectionAtIndex(ulong index)
        {
            GetSectionAtIndexResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetSectionAtIndex(
                        new GetSectionAtIndexRequest
                        {
                            Module = grpcSbModule,
                            Index = index,
                        });
                }))
            {
                if (response.Section != null && response.Section.Id != 0)
                {
                    return sectionFactory.Create(connection, response.Section);
                }
            }
            return null;
        }

        public long GetId()
        {
            return grpcSbModule.Id;
        }

        #endregion
    }
}

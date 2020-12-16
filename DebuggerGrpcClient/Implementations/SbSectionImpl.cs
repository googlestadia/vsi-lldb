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

﻿using Debugger.Common;
using Debugger.SbSectionRpc;
using DebuggerApi;
using System;
using System.Runtime.InteropServices;
using YetiCommon;
using static Debugger.SbSectionRpc.SbSectionRpcService;

namespace DebuggerGrpcClient
{
    // Creates SbSection objects.
    public class GrpcSectionFactory
    {
        public virtual SbSection Create(
            GrpcConnection connection, GrpcSbSection grpcSbSection)
        {
            return new SbSectionImpl(connection, grpcSbSection);
        }
    }

    class SbSectionImpl : SbSection
    {
        readonly GrpcConnection connection;
        readonly GrpcSbSection grpcSbSection;
        readonly SbSectionRpcServiceClient client;
        readonly GCHandle gcHandle;

        internal SbSectionImpl(GrpcConnection connection, GrpcSbSection grpcSbSection)
            : this(connection,
                  new SbSectionRpcServiceClient(connection.CallInvoker),
                  grpcSbSection)
        { }

        internal SbSectionImpl(
            GrpcConnection connection, SbSectionRpcServiceClient client,
            GrpcSbSection grpcSbSection)
        {
            this.connection = connection;
            this.client = client;
            this.grpcSbSection = grpcSbSection;

            // Keep a handle to objects we need in the destructor.
            gcHandle = GCHandle.Alloc(Tuple.Create(connection, client, grpcSbSection));
        }

        ~SbSectionImpl()
        {
            connection.InvokeRpc(() =>
            {
                client.Delete(new DeleteRequest { Section = grpcSbSection });
            });
            gcHandle.Free();
        }

        #region SbSection

        public DebuggerApi.SectionType GetSectionType()
        {
            GetSectionTypeResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetSectionType(
                        new GetSectionTypeRequest { Section = grpcSbSection });
                }))
            {
                return response.SectionType.ConvertTo<DebuggerApi.SectionType>();
            }
            return DebuggerApi.SectionType.Invalid;
        }

        public ulong GetLoadAddress(RemoteTarget target)
        {
            GetLoadAddressResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetLoadAddress(
                        new GetLoadAddressRequest
                        {
                            Section = grpcSbSection,
                            Target = new GrpcSbTarget { Id = target.GetId() }
                        });
                }))
            {
                return response.LoadAddress;
            }
            return DebuggerConstants.INVALID_ADDRESS;
        }

        public ulong GetFileAddress()
        {
            GetFileAddressResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetFileAddress(
                        new GetFileAddressRequest { Section = grpcSbSection });
                }))
            {
                return response.FileAddress;
            }
            return DebuggerConstants.INVALID_ADDRESS;
        }

        public ulong GetFileOffset()
        {
            GetFileOffsetResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.GetFileOffset(
                    new GetFileOffsetRequest { Section = grpcSbSection });
            }))
            {
                return response.FileOffset;
            }
            return DebuggerConstants.INVALID_ADDRESS;
        }

        #endregion
    }
}

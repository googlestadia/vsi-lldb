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
using Debugger.SbUnixSignalsRpc;
using DebuggerApi;
using System;
using System.Runtime.InteropServices;
using SbUnixSignalsRpcServiceClient =
    Debugger.SbUnixSignalsRpc.SbUnixSignalsRpcService.SbUnixSignalsRpcServiceClient;

namespace DebuggerGrpcClient
{
    // Creates SbUnixSignals objects
    public class GrpcUnixSignalsFactory
    {
        public virtual SbUnixSignals Create(
            GrpcConnection connection, GrpcSbUnixSignals grpcSbUnixSignals)
        {
            return new SbUnixSignalsImpl(connection, grpcSbUnixSignals);
        }
    }

    class SbUnixSignalsImpl : SbUnixSignals
    {
        readonly GrpcConnection connection;
        readonly SbUnixSignalsRpcServiceClient client;
        readonly GrpcSbUnixSignals grpcSbUnixSignals;
        readonly GCHandle gcHandle;

        internal SbUnixSignalsImpl(GrpcConnection connection, GrpcSbUnixSignals grpcSbUnixSignals)
        {
            this.connection = connection;
            client = new SbUnixSignalsRpcServiceClient(connection.CallInvoker);
            this.grpcSbUnixSignals = grpcSbUnixSignals;

            // Keep a handle to objects we need in the destructor.
            gcHandle = GCHandle.Alloc(
                new Tuple<GrpcConnection, SbUnixSignalsRpcServiceClient, GrpcSbUnixSignals>(
                    connection, client, grpcSbUnixSignals));
        }

        ~SbUnixSignalsImpl()
        {
            connection.InvokeRpc(() =>
            {
                client.Delete(new DeleteRequest { Signals = grpcSbUnixSignals });
            });
            gcHandle.Free();
        }

        #region SbUnixSignals functions

        public bool GetShouldStop(int signalNumber)
        {
            GetShouldStopResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetShouldStop(
                        new GetShouldStopRequest
                        {
                            Signals = grpcSbUnixSignals,
                            SignalNumber = signalNumber
                        });
                }))
            {
                return response.ShouldStop;
            }
            return false;
        }

        public bool SetShouldStop(int signalNumber, bool value)
        {
            SetShouldStopResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.SetShouldStop(
                        new SetShouldStopRequest
                        {
                            Signals = grpcSbUnixSignals,
                            SignalNumber = signalNumber,
                            Value = value
                        });
                }))
            {
                return response.Result;
            }
            return false;
        }

        #endregion

    }
}

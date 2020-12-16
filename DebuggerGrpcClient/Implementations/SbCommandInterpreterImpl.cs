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
using Debugger.Common;
using DebuggerApi;
using System;
using System.Runtime.InteropServices;
using YetiCommon;
using ReturnStatus = DebuggerApi.ReturnStatus;
using SbCommandInterpreterRpcServiceClient = Debugger.SbCommandInterpreterRpc.SbCommandInterpreterRpcService.SbCommandInterpreterRpcServiceClient;
using System.Diagnostics;

namespace DebuggerGrpcClient
{
    // Creates SbCommandInterpreter objects.
    public class GrpcSbCommandInterpreterFactory
    {
        public virtual SbCommandInterpreter Create(
            GrpcConnection connection, GrpcSbCommandInterpreter grpcSbCommandInterpreter)
        {
            return new SbCommandInterpreterImpl(connection, grpcSbCommandInterpreter);
        }
    }

    class SbCommandInterpreterImpl : SbCommandInterpreter
    {
        readonly GrpcConnection connection;
        readonly SbCommandInterpreterRpcServiceClient client;
        readonly GrpcSbCommandInterpreter grpcSbInterpreter;
        readonly GrpcSbCommandInterpreterFactory commandInterpreterFactory;
        readonly GrpcSbCommandReturnObjectFactory returnObjectFactory;
        readonly GCHandle gcHandle;

        internal SbCommandInterpreterImpl(GrpcConnection connection,
            GrpcSbCommandInterpreter grpcSbInterpreter)
            : this(connection,
                  new SbCommandInterpreterRpcServiceClient(connection.CallInvoker),
                  grpcSbInterpreter, new GrpcSbCommandInterpreterFactory(),
                  new GrpcSbCommandReturnObjectFactory())
        { }

        internal SbCommandInterpreterImpl(
            GrpcConnection connection, SbCommandInterpreterRpcServiceClient client,
            GrpcSbCommandInterpreter grpcSbInterpreter,
            GrpcSbCommandInterpreterFactory commandInterpreterFactory,
            GrpcSbCommandReturnObjectFactory returnObjectFactory)
        {
            this.connection = connection;
            this.client = client;
            this.grpcSbInterpreter = grpcSbInterpreter;
            this.commandInterpreterFactory = commandInterpreterFactory;
            this.returnObjectFactory = returnObjectFactory;

            // Keep a handle to objects we need in the destructor.
            gcHandle = GCHandle.Alloc(
                new Tuple<GrpcConnection,
                          SbCommandInterpreterRpcServiceClient,
                          GrpcSbCommandInterpreter>(
                    connection, client, grpcSbInterpreter));
        }

        ~SbCommandInterpreterImpl()
        {
            connection.InvokeRpc(() =>
            {
                client.Delete(new DeleteRequest { Interpreter = grpcSbInterpreter });
            });
            gcHandle.Free();
        }

        #region SbCommandInterpreter

        public ReturnStatus HandleCommand(string command, out SbCommandReturnObject result)
        {
            result = null;

            HandleCommandResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.HandleCommand(
                        new HandleCommandRequest{ Command = command,
                            Interpreter = grpcSbInterpreter });
                }))
            {
                if (response.ReturnObject != null && response.ReturnObject.Id != 0)
                {
                    result = returnObjectFactory.Create(connection, response.ReturnObject);
                    return response.ReturnStatus.ConvertTo<ReturnStatus>();
                }
            }

            return ReturnStatus.Invalid;
        }

        public void SourceInitFileInHomeDirectory()
        {
            SourceInitFileInHomeDirectoryResponse response = null;
            connection.InvokeRpc(() =>
            {
                response = client.SourceInitFileInHomeDirectory(
                    new SourceInitFileInHomeDirectoryRequest
                    {
                        Interpreter = grpcSbInterpreter
                    });
            });
        }
        #endregion
    }

    /// <summary>
    /// Extension methods for SbCommandInterpreter.
    /// These are extension methods, instead of being added directly to SbCommandInterpreter as
    /// there is no corresponding API, and SbCommandInterpreter is meant to be a very simple
    /// wrapper.
    /// </summary>
    public static class SbCommandInterpreterExtensions
    {
        public static void HandleAndLogCommand(this SbCommandInterpreter commandInterpreter,
            string command)
        {
            SbCommandReturnObject commandResult;
            commandInterpreter.HandleCommand(command, out commandResult);
            if (commandResult == null)
            {
                Trace.WriteLine(
                    $"WARNING: The LLDB command '{command}' failed to return a result.");
                return;
            }
            Trace.WriteLine($"Executed LLDB command '{command}' with result: " +
                $"{Environment.NewLine} {commandResult.GetDescription()}" +
                $"{Environment.NewLine} Command error: {commandResult.GetError()}" +
                $"{Environment.NewLine} Command output: {commandResult.GetOutput()}");
        }
    }
}

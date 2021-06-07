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
using Debugger.RemoteFrameRpc;
using DebuggerApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using YetiCommon;
using DebuggerCommonApi;
using System.Threading.Tasks;

namespace DebuggerGrpcClient
{
    // Creates RemoteFrameProxy objects.
    public class RemoteFrameProxyFactory
    {
        public virtual RemoteFrame Create(GrpcConnection connection, GrpcSbFrame grpcSbFrame)
        {
            if (grpcSbFrame == null)
            {
                return null;
            }
            return new RemoteFrameProxy(connection, grpcSbFrame);
        }

        public virtual void Delete(GrpcConnection connection, GrpcSbFrame grpcSbFrame)
        {
            var client = new RemoteFrameRpcService.RemoteFrameRpcServiceClient(
                connection.CallInvoker);
            connection
                .GetOrCreateBulkDeleter<GrpcSbFrame>()
                .QueueForDeletion(grpcSbFrame, (List<GrpcSbFrame> frames) =>
                {
                    var request = new BulkDeleteRequest();
                    request.Frames.AddRange(frames);
                    connection.InvokeRpc(() =>
                    {
                        client.BulkDelete(request);
                    });
                });
        }
    }

    // Implementation of the RemoteFrame interface that uses GRPC to make RPCs to a remote endpoint.
    public class RemoteFrameProxy : RemoteFrame, IDisposable
    {
        readonly GrpcConnection connection;
        readonly GrpcSbFrame grpcSbFrame;
        readonly RemoteFrameRpcService.RemoteFrameRpcServiceClient client;
        readonly GrpcModuleFactory moduleFactory;
        readonly GrpcThreadFactory threadFactory;
        readonly GrpcValueFactory valueFactory;
        readonly GrpcFunctionFactory functionFactory;
        readonly GrpcSymbolFactory symbolFactory;
        readonly GCHandle gcHandle;

        private bool disposed = false;

        internal RemoteFrameProxy(GrpcConnection connection, GrpcSbFrame grpcSbFrame)
            : this(connection, grpcSbFrame,
                   new RemoteFrameRpcService.RemoteFrameRpcServiceClient(connection.CallInvoker),
                   new GrpcModuleFactory(), new GrpcThreadFactory(), new GrpcValueFactory(),
                   new GrpcFunctionFactory(), new GrpcSymbolFactory())
        { }

        internal RemoteFrameProxy(GrpcConnection connection, GrpcSbFrame grpcSbFrame,
                                  RemoteFrameRpcService.RemoteFrameRpcServiceClient client,
                                  GrpcModuleFactory moduleFactory, GrpcThreadFactory threadFactory,
                                  GrpcValueFactory valueFactory,
                                  GrpcFunctionFactory functionFactory,
                                  GrpcSymbolFactory symbolFactory)
        {
            this.connection = connection;
            this.grpcSbFrame = grpcSbFrame;
            this.client = client;
            this.moduleFactory = moduleFactory;
            this.threadFactory = threadFactory;
            this.valueFactory = valueFactory;
            this.functionFactory = functionFactory;
            this.symbolFactory = symbolFactory;

            // Keep a handle to objects we need in the destructor.
            gcHandle = GCHandle.Alloc(
                new Tuple<
                    GrpcConnection, RemoteFrameRpcService.RemoteFrameRpcServiceClient, GrpcSbFrame>
                    (connection, client, grpcSbFrame));
        }

        ~RemoteFrameProxy()
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
                    .GetOrCreateBulkDeleter<GrpcSbFrame>()
                    .QueueForDeletion(grpcSbFrame, (List<GrpcSbFrame> frames) =>
                    {
                        var request = new BulkDeleteRequest();
                        request.Frames.AddRange(frames);
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

        #region RemoteFrame

        public async Task<RemoteValue> EvaluateExpressionAsync(string text)
        {
            EvaluateExpressionResponse response = null;
            if (await connection.InvokeRpcAsync(async delegate
            {
                response = await client.EvaluateExpressionAsync(
                    new EvaluateExpressionRequest() { Frame = grpcSbFrame, Expression = text });
            }))
            {
                if (response.Value != null && response.Value.Id != 0)
                {
                    return valueFactory.Create(connection, response.Value);
                }
            }
            return null;
        }

        public async Task<RemoteValue> EvaluateExpressionLldbEvalAsync(string text)
        {
            EvaluateExpressionLldbEvalResponse response = null;
            if (await connection.InvokeRpcAsync(async delegate
            {
                var request = new EvaluateExpressionLldbEvalRequest()
                {
                    Frame = grpcSbFrame,
                    Expression = text
                };
                response = await client.EvaluateExpressionLldbEvalAsync(request);
            }))
            {
                if (response.Value != null && response.Value.Id != 0)
                {
                    return valueFactory.Create(connection, response.Value);
                }
            }

            return null;
        }

        public SbFunction GetFunction()
        {
            GetFunctionResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetFunction(
                        new GetFunctionRequest() { Frame = grpcSbFrame });
                }))
            {
                if (response.Function != null && response.Function.Id != 0)
                {
                    return functionFactory.Create(connection, response.Function);
                }
            }
            return null;
        }

        public string GetFunctionName() => grpcSbFrame.FunctionName;

        public string GetFunctionNameWithSignature() => grpcSbFrame.FunctionNameWithSignature;

        public LineEntryInfo GetLineEntry() =>
            FrameInfoUtils.CreateLineEntryInfo(grpcSbFrame.LineEntry);

        public SbModule GetModule()
        {
            GetModuleResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetModule(new GetModuleRequest() { Frame = grpcSbFrame });
                }))
            {
                if (response.Module != null && response.Module.Id != 0)
                {
                    return moduleFactory.Create(connection, response.Module);
                }
            }
            return null;
        }

        public ulong GetPC()
        {
            return grpcSbFrame.ProgramCounter;
        }

        public List<RemoteValue> GetRegisters()
        {
            GetRegistersResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetRegisters(
                        new GetRegistersRequest() { Frame = grpcSbFrame });
                }))
            {
                return response.Registers.Select(s => valueFactory.Create(connection, s)).ToList();
            }
            return new List<RemoteValue>();
        }

        public SbSymbol GetSymbol()
        {
            GetSymbolResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetSymbol(new GetSymbolRequest() { Frame = grpcSbFrame });
                }))
            {
                if (response.Symbol != null && response.Symbol.Id != 0)
                {
                    return symbolFactory.Create(connection, response.Symbol);
                }
            }
            return null;
        }

        public RemoteThread GetThread()
        {
            GetThreadResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetThread(new GetThreadRequest() { Frame = grpcSbFrame });
                }))
            {
                return threadFactory.Create(connection, response.Thread);
            }
            return null;
        }

        public List<RemoteValue> GetVariables(bool arguments, bool locals, bool statics,
            bool onlyInScope)
        {
            var request = new GetVariablesRequest
            {
                Frame = grpcSbFrame,
                Arguments = arguments,
                Locals = locals,
                Statics = statics,
                OnlyInScope = onlyInScope
            };
            GetVariablesResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetVariables(request);
                }))
            {
                return response.Variables.Select(s => valueFactory.Create(connection, s)).ToList();
            }
            return new List<RemoteValue>();
        }

        public RemoteValue GetValueForVariablePath(string varPath)
        {
            var request = new GetValueForVariablePathRequest
            {
                Frame = grpcSbFrame,
                VariablePath = varPath
            };
            GetValueForVariablePathResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetValueForVariablePath(request);
                }))
            {
                if (response.Value != null && response.Value.Id != 0)
                    return valueFactory.Create(connection, response.Value);
            }
            return null;
        }

        public RemoteValue FindValue(string varName, DebuggerApi.ValueType value_type)
        {
            var request = new FindValueRequest
            {
                Frame = grpcSbFrame,
                VariableName = varName,
                ValueType = value_type.ConvertTo<Debugger.Common.ValueType>(),
            };
            FindValueResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.FindValue(request);
            }))
            {
                if (response.Variable != null && response.Variable.Id != 0)
                    return valueFactory.Create(connection, response.Variable);
            }
            return null;
        }

        public AddressRange GetPhysicalStackRange()
        {
            var request = new GetPhysicalStackRangeRequest() { Frame = grpcSbFrame };
            GetPhysicalStackRangeResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.GetPhysicalStackRange(request);
            }))
            {
                if (response.AddressRange != null)
                {
                    return new AddressRange(response.AddressRange.AddressMin,
                        response.AddressRange.AddressMax);
                }
            }
            return null;
        }

        public FrameInfo<SbModule>? GetInfo(FrameInfoFlags fields)
        {
            var request = new GetInfoRequest()
            {
                Frame = grpcSbFrame,
                Fields = (uint)fields
            };
            GetInfoResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.GetInfo(request);
            }))
            {
                if (response.Info != null)
                {
                    return FrameInfoUtils.CreateFrameInfo(response.Info, moduleFactory, connection);
                }
            }
            return null;
        }

        #endregion
    }
}

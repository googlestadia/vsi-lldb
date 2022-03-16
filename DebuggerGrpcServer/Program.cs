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

using Debugger.SbAddressRpc;
using Debugger.SbBreakpointLocationRpc;
using Debugger.RemoteBreakpointRpc;
using Debugger.SbCommandInterpreterRpc;
using Debugger.SbCommandReturnObjectRpc;
using Debugger.SbDebuggerRpc;
using Debugger.RemoteFrameRpc;
using Debugger.SbFunctionRpc;
using Debugger.SbListenerRpc;
using Debugger.SbModuleRpc;
using Debugger.SbPlatformRpc;
using Debugger.SbProcessRpc;
using Debugger.SbSymbolRpc;
using Debugger.RemoteTargetRpc;
using Debugger.RemoteThreadRpc;
using Debugger.SbTypeMemberRpc;
using Debugger.SbTypeRpc;
using Debugger.SbUnixSignalsRpc;
using Debugger.RemoteValueRpc;
using Debugger.SbWatchpointRpc;
using Debugger.SbSectionRpc;
using Grpc.Core;
using Grpc.Core.Logging;
using LldbApi;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using YetiVSI.DebugEngine;
using System.Linq;
using System.Threading.Tasks;

namespace DebuggerGrpcServer
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            CommandLineOptions opts;
            return CommandLineOptions.TryParse(args, out opts) ? await RunServerAsync(opts) : -1;
        }

        static async Task<int> RunServerAsync(CommandLineOptions opts)
        {
            // Create pipes.
            string[] inPipeHandles = opts.InPipeHandles.ToArray();
            string[] outPipeHandles = opts.OutPipeHandles.ToArray();
            int numPipes = inPipeHandles.Length;
            if (numPipes != outPipeHandles.Length)
            {
                return -1;
            }

            GrpcEnvironment.SetLogger(new ConsoleLogger());

            // Re-direct trace logging to the console.
            ConsoleTraceListener consoleTraceListener = new ConsoleTraceListener();
            consoleTraceListener.Name = "mainConsoleTracer";
            Trace.Listeners.Add(consoleTraceListener);

            var listenerManager = new ConcurrentDictionary<long, SbListener>();
            var targetManager = new ConcurrentDictionary<long, RemoteTarget>();
            var processManager = new ConcurrentDictionary<int, SbProcess>();
            var valueManager = new ObjectStore<RemoteValue>();
            var addressManager = new ObjectStore<SbAddress>();
            var functionManager = new ObjectStore<SbFunction>();
            var symbolManager = new ObjectStore<SbSymbol>();
            var typeManager = new ObjectStore<SbType>();
            var typeMemberManager = new ObjectStore<SbTypeMember>();
            var moduleManager = new UniqueObjectStore<SbModule>(SbModuleEqualityComparer.Instance);
            var instructionManager = new ObjectStore<SbInstruction>();
            var watchpointManager = new ObjectStore<SbWatchpoint>();
            var commandInterpreterManager = new ObjectStore<SbCommandInterpreter>();
            var commandReturnObjectManager = new ObjectStore<SbCommandReturnObject>();
            var unixSignalsManager = new ObjectStore<SbUnixSignals>();
            var frameManager = new ObjectStore<RemoteFrame>();
            var threadManager = new ObjectStore<RemoteThread>();
            var sectionManager = new ObjectStore<SbSection>();

            var lldbExpressionOptionsFactory = new LLDBExpressionOptionsFactory();
            var remoteValueFactory = new RemoteValueImpl.Factory(lldbExpressionOptionsFactory);
            var remoteFrameFactory = new RemoteFrameImpl.Factory(
                remoteValueFactory, lldbExpressionOptionsFactory);

            var remoteThreadFactory = new RemoteThreadImpl.Factory(remoteFrameFactory);
            remoteFrameFactory.SetRemoteThreadFactory(remoteThreadFactory);

            var fileSpecFactory = new LLDBFileSpecFactory();

            var sbDebuggerRpc = new SbDebuggerRpcServiceImpl(targetManager,
                commandInterpreterManager);
            var sbCommandInterpreterRpc = new SbCommandInterpreterRpcServiceImpl(
                commandInterpreterManager, commandReturnObjectManager);
            var sbCommandReturnObjectRpc = new SbCommandReturnObjectServiceImpl(
                commandReturnObjectManager);
            var sbPlatformRpc = new SbPlatformRpcServiceImpl();
            var remoteTargetRpc = new RemoteTargetRpcServiceImpl(
                targetManager, listenerManager, processManager, moduleManager, watchpointManager,
                addressManager, typeManager);
            var sbListenerRpc = new SbListenerRpcServiceImpl(listenerManager);

            var sbProcessRpc = new SbProcessRpcServiceImpl(processManager, threadManager,
                remoteThreadFactory, unixSignalsManager);
            var remoteThreadRpc = new RemoteThreadRpcServiceImpl(
                processManager, threadManager, frameManager, moduleManager);
            var remoteBreakpointRpc = new RemoteBreakpointRpcServiceImpl(targetManager);
            var sbUnixSignalsRpc = new SbUnixSignalsRpcServiceImpl(unixSignalsManager);
            var sbWatchpointRpc = new SbWatchpointRpcServiceImpl(watchpointManager);
            var sbBreakpointLocationRpc = new SbBreakpointLocationRpcServiceImpl(targetManager,
                addressManager);
            var remoteFrameRpc =
                new RemoteFrameRpcServiceImpl(valueManager, functionManager, symbolManager,
                                              moduleManager, frameManager, threadManager);
            var remoteValueRpc = new RemoteValueRpcServiceImpl(valueManager, typeManager);
            var sbTypeRpc = new SbTypeRpcServiceImpl(typeManager, typeMemberManager);
            var sbTypeMemberRpc = new SbTypeMemberRpcServiceImpl(typeMemberManager, typeManager);
            var sbAddressRpc = new SbAddressRpcServiceImpl(addressManager, targetManager,
                functionManager, symbolManager);
            var sbFunctionRpc = new SbFunctionRpcServiceImpl(
                addressManager, functionManager, instructionManager, targetManager);
            var sbSymbolRpc = new SbSymbolRpcServiceImpl(addressManager, symbolManager);
            var sbModuleRpc = new SbModuleRpcServiceImpl(moduleManager, addressManager,
                                                         sectionManager, fileSpecFactory);
            var sbSectionRpc = new SbSectionRpcServiceImpl(sectionManager, targetManager);

            sbDebuggerRpc.Initialize(sbPlatformRpc);

            PipeServiceBinder server = new PipeServiceBinder(inPipeHandles, outPipeHandles);

            SbDebuggerRpcService.BindService(server, sbDebuggerRpc);
            SbCommandInterpreterRpcService.BindService(server, sbCommandInterpreterRpc);
            SbCommandReturnObjectRpcService.BindService(server, sbCommandReturnObjectRpc);
            RemoteTargetRpcService.BindService(server, remoteTargetRpc);
            SbListenerRpcService.BindService(server, sbListenerRpc);
            SbPlatformRpcService.BindService(server, sbPlatformRpc);
            SbProcessRpcService.BindService(server, sbProcessRpc);
            RemoteThreadRpcService.BindService(server, remoteThreadRpc);
            RemoteBreakpointRpcService.BindService(server, remoteBreakpointRpc);
            SbBreakpointLocationRpcService.BindService(server, sbBreakpointLocationRpc);
            RemoteFrameRpcService.BindService(server, remoteFrameRpc);
            RemoteValueRpcService.BindService(server, remoteValueRpc);
            SbTypeRpcService.BindService(server, sbTypeRpc);
            SbTypeMemberRpcService.BindService(server, sbTypeMemberRpc);
            SbAddressRpcService.BindService(server, sbAddressRpc);
            SbFunctionRpcService.BindService(server, sbFunctionRpc);
            SbSymbolRpcService.BindService(server, sbSymbolRpc);
            SbModuleRpcService.BindService(server, sbModuleRpc);
            SbWatchpointRpcService.BindService(server, sbWatchpointRpc);
            SbUnixSignalsRpcService.BindService(server, sbUnixSignalsRpc);
            SbSectionRpcService.BindService(server, sbSectionRpc);

            server.Start();
            Console.WriteLine("LLDB GRPC server listening to the pipes");
            await server.ShutdownTask;
            Console.WriteLine("Shutdown complete.");
            return 0;
        }
    }
}

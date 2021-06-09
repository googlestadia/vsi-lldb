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

using Debugger.SbDebuggerRpc;
using DebuggerApi;
using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace DebuggerGrpcClient
{
    /// <summary>
    /// Creates SBDebugger objects.
    /// </summary>
    public class GrpcDebuggerFactory
    {
        public virtual SbDebugger Create(
            GrpcConnection connection, bool sourceInitFiles, TimeSpan retryWaitTime)
        {
            if (connection == null) { throw new ArgumentNullException(nameof(connection));}
            if (retryWaitTime == null) { throw new ArgumentNullException(nameof(retryWaitTime)); }

            var instance = new SbDebuggerImpl(connection);
            if (instance.Create(sourceInitFiles, retryWaitTime))
            {
                return instance;
            }
            return null;
        }
    }

    /// <summary>
    /// Implementation of the SBDebugger interface that uses GRPC to make RPCs to a remote endpoint.
    /// </summary>
    class SbDebuggerImpl : SbDebugger
    {
        readonly SbDebuggerRpcService.SbDebuggerRpcServiceClient client;
        readonly GrpcConnection connection;
        readonly GrpcTargetFactory sbTargetFactory;
        readonly GrpcPlatformFactory sbPlatformFactory;
        readonly GrpcSbCommandInterpreterFactory sbCommandInterpreterFactory;

        internal SbDebuggerImpl(GrpcConnection connection)
            : this(connection, new GrpcTargetFactory(), new GrpcPlatformFactory(),
                  new GrpcSbCommandInterpreterFactory(),
                  new SbDebuggerRpcService.SbDebuggerRpcServiceClient(connection.CallInvoker)) {}

        // Constructor that can be used by tests to pass in mock objects.
        internal SbDebuggerImpl(GrpcConnection connection, GrpcTargetFactory sbTargetFactory,
            GrpcPlatformFactory sbPlatformFactory,
            GrpcSbCommandInterpreterFactory sbCommandInterpreterFactory,
            SbDebuggerRpcService.SbDebuggerRpcServiceClient client)
        {
            this.connection = connection;
            this.sbTargetFactory = sbTargetFactory;
            this.sbPlatformFactory = sbPlatformFactory;
            this.sbCommandInterpreterFactory = sbCommandInterpreterFactory;
            this.client = client;
        }

        internal bool Create(bool sourceInitFiles, TimeSpan retryWaitTime)
        {
            return connection.InvokeRpc(() =>
                {
                    client.Create(
                        new CreateRequest { SourceInitFiles = sourceInitFiles },
                        new CallOptions()
                            .WithDeadline(DateTime.UtcNow.Add(retryWaitTime))
                            .WithWaitForReady());
                });
        }

        #region SbDebugger

        public void SetAsync(bool async)
        {
            connection.InvokeRpc(() =>
                {
                    client.SetAsync(new SetAsyncRequest { Async = async });
                });
        }

        public void SkipLLDBInitFiles(bool skip)
        {
            connection.InvokeRpc(() =>
                {
                    client.SkipLLDBInitFiles(
                        new SkipLLDBInitFilesRequest { Skip = skip });
                });
        }

        public SbCommandInterpreter GetCommandInterpreter()
        {
            GetCommandInterpreterResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetCommandInterpreter(
                        new GetCommandInterpreterRequest());
                }))
            {
                if (response.Interpreter != null && response.Interpreter.Id != 0)
                {
                    return sbCommandInterpreterFactory.Create(connection, response.Interpreter);
                }
            }
            return null;
        }

        public RemoteTarget CreateTarget(string filename)
        {
            CreateTargetResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.CreateTarget(
                        new CreateTargetRequest { Filename = filename });
                }))
            {
                return sbTargetFactory.Create(connection, response.GrpcSbTarget);
            }
            return null;
        }

        public void SetSelectedPlatform(SbPlatform platform)
        {
            // We only support a single platform, so don't bother passing a platform here as the
            // server just assumes which platform to use.
            connection.InvokeRpc(() =>
                {
                    client.SetSelectedPlatform(new SetSelectedPlatformRequest());
                });
        }

        public SbPlatform GetSelectedPlatform()
        {
            var request = new GetSelectedPlatformRequest();
            if (connection.InvokeRpc(() =>
                {
                    client.GetSelectedPlatform(request);
                }))
            {
                // Create a new platform without telling the server to create a new one.  Since we
                // only support a single platform on the server, there is no need to identify which
                // platform to call the RPCs on (its assumed).  So just create a default platform
                // and start calling RPCs.
                return sbPlatformFactory.Create(connection);
            }
            return null;
        }

        public bool EnableLog(string channel, IEnumerable<string> types)
        {
            var request = new EnableLogRequest { Channel = channel };
            request.Types_.Add(types);
            EnableLogResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.EnableLog(request);
                }))
            {
                return response.Result;
            }
            return false;
        }

        public bool IsPlatformAvailable(string platformName)
        {
            var request = new IsPlatformAvailableRequest()
            {
                PlatformName = platformName
            };

            IsPlatformAvailableResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.IsPlatformAvailable(request);
            }))
            {
                return response.Result;
            }
            return false;
        }

        public bool DeleteTarget(RemoteTarget target)
        {
            // Not needed as the external process shutdown will take care of cleanup.
            return true;
        }

        #endregion
    }

    /// <summary>
    /// Extension methods for SbDebugger commands.
    /// These are extension methods, instead of being added directly to SbDebugger as there is no
    /// corresponding API, and SbDebugger is meant to be a very simple wrapper.
    /// </summary>
    public static class SbDebuggerExtensions
    {
        public const string FastExpressionEvaluationCommand =
            "settings set target.experimental.inject-local-vars false";
        static string CreateSetLibrarySearchPathCommand(string path)
            => $"settings append target.exec-search-paths \"{path}\"";

        static string[] DefaultLLDBSettings = {
            // Do not auto apply fixits because they could cause unintended
            // side-effects. For example, a fixit on function name 'f' is
            // a call to function f, i.e. 'f()'.
            "settings set target.auto-apply-fixits false",
            // Replace the default memory chunk size 512 with a higher one
            // so that we decrease the number of roundtrips to the remote
            // machine.
            "settings set target.process.memory-cache-line-size 4096",
            // Turn on template instantiation in expressions.
            "settings set target.experimental.infer-class-templates true",
            // Set gdb-remote packet timeout to 5s so that big messages
            // have enough time to get through on slower networks.
            "settings set plugin.process.gdb-remote.packet-timeout 5",
            // Enable the dynamic loader plugin for debugging Wine.
            "settings set plugin.process.wine-dyld.use-wine-dynamic-loader true",
            // Set the correct path to llvm-objdump on the target.
            "settings set plugin.process.wine-dyld.remote-objdump-path \"" +
                YetiCommon.YetiConstants.RemoteLlvmObjDumpPath + "\"",
        };

        public static string GetLLDBInitPath()
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(homeDir, ".lldbinit");
        }

        public static void EnableFastExpressionEvaluation(this SbDebugger debugger)
        {
            Trace.WriteLine("Enabling fast expression evaluation.");
            debugger.GetCommandInterpreter().HandleAndLogCommand(FastExpressionEvaluationCommand);
        }
        public static void SetLibrarySearchPath(this SbDebugger debugger, string path)
        {
            debugger.GetCommandInterpreter().HandleAndLogCommand(
                CreateSetLibrarySearchPathCommand(path));
        }
        public static void SetDefaultLLDBSettings(this SbDebugger debugger)
        {
            foreach (string s in DefaultLLDBSettings)
            {
                debugger.GetCommandInterpreter().HandleAndLogCommand(s);
            }
        }
    }
}

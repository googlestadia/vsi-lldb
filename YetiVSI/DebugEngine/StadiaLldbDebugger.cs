// Copyright 2021 Google LLC
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

using DebuggerApi;
using DebuggerGrpcClient;
using Microsoft.VisualStudio;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using YetiVSI.DebuggerOptions;
using YetiVSI.Metrics;
using static YetiVSI.DebugEngine.DebugEngine;

namespace YetiVSI.DebugEngine
{
    public interface IStadiaLldbDebuggerFactory
    {
        StadiaLldbDebugger Create(GrpcConnection grpcConnection,
                                  DebuggerOptions.DebuggerOptions debuggerOptions,
                                  HashSet<string> libPaths, string executableFullPath,
                                  bool attachToCore);
    }

    public class StadiaLldbDebugger
    {
        public class Factory : IStadiaLldbDebuggerFactory
        {
            readonly GrpcDebuggerFactory _lldbDebuggerFactory;
            readonly GrpcPlatformFactory _lldbPlatformFactory;
            readonly IFileSystem _fileSystem;
            readonly ActionRecorder _actionRecorder;
            readonly bool _fastExpressionEvaluation;

            public Factory(GrpcDebuggerFactory lldbDebuggerFactory,
                           GrpcPlatformFactory lldbPlatformFactory, IFileSystem fileSystem,
                           ActionRecorder actionRecorder, bool fastExpressionEvaluation)
            {
                _lldbDebuggerFactory = lldbDebuggerFactory;
                _lldbPlatformFactory = lldbPlatformFactory;
                _fileSystem = fileSystem;
                _actionRecorder = actionRecorder;
                _fastExpressionEvaluation = fastExpressionEvaluation;
            }

            public StadiaLldbDebugger Create(GrpcConnection grpcConnection,
                                             DebuggerOptions.DebuggerOptions debuggerOptions,
                                             HashSet<string> libPaths, string executableFullPath,
                                             bool attachToCore)
            {
                // This should be the first request to the DebuggerGrpcServer.  Providing a retry
                // wait time allows us to connect to a DebuggerGrpcServer that is slow to start.
                // Note that we postpone sourcing .lldbinit until we are done with our
                // initialization so that the users can override our defaults.
                SbDebugger debugger =
                    _lldbDebuggerFactory.Create(grpcConnection, false, TimeSpan.FromSeconds(10));

                if (debugger == null)
                {
                    throw new AttachException(VSConstants.E_ABORT,
                                              ErrorStrings.FailedToCreateDebugger);
                }

                if (debuggerOptions[DebuggerOption.CLIENT_LOGGING] == DebuggerOptionState.ENABLED)
                {
                    debugger.EnableLog("lldb", new List<string> { "default", "module" });

                    // TODO: Disable 'dwarf' logs until we can determine why this
                    // causes LLDB to hang.
                    // lldbDebugger.EnableLog("dwarf", new List<string> { "default" });
                }

                if (_fastExpressionEvaluation)
                {
                    debugger.EnableFastExpressionEvaluation();
                }

                debugger.SetDefaultLLDBSettings();

                // Apply .lldbinit after we set our settings so that the user can override our
                // defaults with a custom .lldbinit.
                LoadLocalLldbInit(debugger);

                // Add exec search paths, so that LLDB can find the executable and any dependent
                // libraries.  If LLDB is able to find the files locally, it won't try to download
                // them from the remote server, saving valuable time on attach.
                foreach (string path in libPaths)
                {
                    debugger.SetLibrarySearchPath(path);
                }

                debugger.SetAsync(true);

                RemoteTarget target = null;
                if (!attachToCore && !string.IsNullOrEmpty(executableFullPath))
                {
                    var createExecutableTargetAction =
                        _actionRecorder.CreateToolAction(
                            ActionType.DebugCreateExecutableTarget);
                    createExecutableTargetAction.Record(
                        () => target = CreateTarget(executableFullPath, debugger));
                }
                else
                {
                    target = CreateTarget("", debugger);
                }

                return new StadiaLldbDebugger(debugger, target, attachToCore, _lldbPlatformFactory);
            }

            RemoteTarget CreateTarget(string executable, SbDebugger debugger)
            {
                RemoteTarget lldbTarget = debugger.CreateTarget(executable);
                if (lldbTarget == null)
                {
                    throw new AttachException(VSConstants.E_ABORT,
                                              ErrorStrings.FailedToCreateDebugTarget);
                }

                return lldbTarget;
            }

            /// <summary>
            /// LoadLocalLldbInit looks for a local LLDB config (~/.lldbinit), logs its contents and
            /// then issues RPCs to load it in LLDB.  Internally LLDB will try to load one of the
            /// following files: ~/.lldbinit-{PROGRAM_NAME}, ~/.lldbinit, {CWD}/.lldbinit (in that
            /// order).  We check only for ~/.lldbinit and don't call
            /// `SourceInitFileInHomeDirectory` if it doesn't exist.
            /// </summary>
            void LoadLocalLldbInit(SbDebugger debugger)
            {
                var lldbinitPath = SbDebuggerExtensions.GetLLDBInitPath();
                string lldbinit;
                try
                {
                    lldbinit = _fileSystem.File.ReadAllText(lldbinitPath);
                }
                catch (FileNotFoundException)
                {
                    Trace.WriteLine("No local ~/.lldbinit found, don't try loading it in LLDB.");
                    return;
                }
                catch (Exception e)
                {
                    Trace.WriteLine($"Unexpected error while reading {lldbinitPath}: {e}");
                    return;
                }

                Trace.WriteLine($"Found ~/.lldbinit ({lldbinitPath}), LLDB will try to load it:" +
                                $"{Environment.NewLine}{lldbinit}{Environment.NewLine}EOF");

                debugger.SkipLLDBInitFiles(false);
                debugger.GetCommandInterpreter().SourceInitFileInHomeDirectory();
            }
        }

        const string _remoteLldbPlatformName = "remote-stadia";
        const string _fallbackRemoteLldbPlatformName = "remote-linux";
        const string _localLldbPlatformName = "host";

        readonly bool _attachToCore;
        readonly GrpcPlatformFactory _lldbPlatformFactory;

        public SbDebugger Debugger { get; }

        public RemoteTarget Target { get; }

        public StadiaLldbDebugger(SbDebugger debugger, RemoteTarget target, bool attachToCore,
                                  GrpcPlatformFactory lldbPlatformFactory)
        {
            Debugger = debugger;
            Target = target;
            _attachToCore = attachToCore;
            _lldbPlatformFactory = lldbPlatformFactory;
        }

        public SbPlatform CreatePlatform(GrpcConnection grpcConnection)
        {
            SbPlatform platform;
            if (_attachToCore)
            {
                platform = _lldbPlatformFactory.Create(_localLldbPlatformName, grpcConnection);
            }
            else
            {
                var platformName = IsStadiaPlatformAvailable()
                    ? _remoteLldbPlatformName : _fallbackRemoteLldbPlatformName;
                platform = _lldbPlatformFactory.Create(platformName, grpcConnection);
            }

            if (platform == null)
            {
                throw new AttachException(VSConstants.E_FAIL,
                                          ErrorStrings.FailedToCreateLldbPlatform);
            }

            return platform;
        }

        public bool IsStadiaPlatformAvailable() =>
            Debugger.IsPlatformAvailable(_remoteLldbPlatformName);
    }
}

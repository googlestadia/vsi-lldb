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

using System;
using System.IO;
using DebuggerApi;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using YetiCommon;
using YetiCommon.CastleAspects;
using YetiVSI.Metrics;

namespace YetiVSI.DebugEngine
{
    public class DebugModule : SimpleDecoratorSelf<IDebugModule3>, IDebugModule3
    {
        public class Factory
        {
            readonly ActionRecorder _actionRecorder;
            readonly CancelableTask.Factory _cancelableTaskFactory;
            readonly ModuleFileLoadMetricsRecorder.Factory _moduleFileLoadRecorderFactory;
            readonly ISymbolSettingsProvider _symbolSettingsProvider;

            [Obsolete("This constructor only exists to support mocking libraries.", true)]
            protected Factory()
            {
            }

            public Factory(CancelableTask.Factory cancelableTaskFactory,
                           ActionRecorder actionRecorder,
                           ModuleFileLoadMetricsRecorder.Factory moduleFileLoadRecorderFactory,
                           ISymbolSettingsProvider symbolSettingsProvider)
            {
                _cancelableTaskFactory = cancelableTaskFactory;
                _actionRecorder = actionRecorder;
                _symbolSettingsProvider = symbolSettingsProvider;
                _moduleFileLoadRecorderFactory = moduleFileLoadRecorderFactory;
            }

            public virtual IDebugModule3 Create(
                IModuleFileLoader moduleFileLoader, IModuleSearchLogHolder moduleSearchLogHolder,
                SbModule lldbModule, uint loadOrder, IDebugEngineHandler debugEngineHandler,
                IGgpDebugProgram program) => new DebugModule(_cancelableTaskFactory,
                                                             _actionRecorder,
                                                             _moduleFileLoadRecorderFactory,
                                                             moduleFileLoader,
                                                             moduleSearchLogHolder, lldbModule,
                                                             loadOrder, debugEngineHandler, program,
                                                             _symbolSettingsProvider);
        }

        readonly ActionRecorder _actionRecorder;

        readonly CancelableTask.Factory _cancelableTaskFactory;
        readonly IDebugEngineHandler _engineHandler;

        readonly SbModule _lldbModule;
        readonly uint _loadOrder;
        readonly IModuleFileLoader _moduleFileLoader;
        readonly ModuleFileLoadMetricsRecorder.Factory _moduleFileLoadRecorderFactory;
        readonly IModuleSearchLogHolder _moduleSearchLogHolder;
        readonly IGgpDebugProgram _program;
        readonly ISymbolSettingsProvider _symbolSettingsProvider;

        string ModuleName => _lldbModule.GetPlatformFileSpec()?.GetFilename() ?? "<unknown>";

        DebugModule(CancelableTask.Factory cancelableTaskFactory, ActionRecorder actionRecorder,
                    ModuleFileLoadMetricsRecorder.Factory moduleFileLoadRecorderFactory,
                    IModuleFileLoader moduleFileLoader,
                    IModuleSearchLogHolder moduleSearchLogHolder, SbModule lldbModule,
                    uint loadOrder, IDebugEngineHandler engineHandler, IGgpDebugProgram program,
                    ISymbolSettingsProvider symbolSettingsProvider)
        {
            _cancelableTaskFactory = cancelableTaskFactory;
            _actionRecorder = actionRecorder;
            _moduleFileLoadRecorderFactory = moduleFileLoadRecorderFactory;
            _moduleFileLoader = moduleFileLoader;
            _moduleSearchLogHolder = moduleSearchLogHolder;
            _lldbModule = lldbModule;
            _loadOrder = loadOrder;
            _engineHandler = engineHandler;
            _program = program;
            _symbolSettingsProvider = symbolSettingsProvider;
        }

        #region IDebugModule2 functions

        public int GetInfo(enum_MODULE_INFO_FIELDS fields, MODULE_INFO[] moduleInfo)
        {
            var info = new MODULE_INFO();
            SbFileSpec platformFileSpec = _lldbModule.GetPlatformFileSpec();
            if (platformFileSpec != null)
            {
                // Name
                if (HasFlag(enum_MODULE_INFO_FIELDS.MIF_NAME))
                {
                    info.m_bstrName = platformFileSpec.GetFilename();
                    info.dwValidFields |= enum_MODULE_INFO_FIELDS.MIF_NAME;
                }

                // Path
                if (HasFlag(enum_MODULE_INFO_FIELDS.MIF_URL))
                {
                    // The module paths are for remote files (on Linux) when attaching to a game,
                    // and for local paths for the postmortem debugging.
                    string directory = platformFileSpec.GetDirectory();
                    string filename = platformFileSpec.GetFilename();
                    info.m_bstrUrl = directory.Contains(@"\")
                        ? Path.Combine(directory, filename)
                        : FileUtil.PathCombineLinux(directory, filename);
                    info.dwValidFields |= enum_MODULE_INFO_FIELDS.MIF_URL;
                }
            }

            // SymbolStatus
            if (HasFlag(enum_MODULE_INFO_FIELDS.MIF_DEBUGMESSAGE))
            {
                if (!_lldbModule.HasSymbolsLoaded())
                {
                    info.m_bstrDebugMessage =
                        "Symbols not loaded. Check 'Symbol Load Information...' for details.";
                    info.dwValidFields |= enum_MODULE_INFO_FIELDS.MIF_DEBUGMESSAGE;
                }
            }

            // Address (range start)
            if (HasFlag(enum_MODULE_INFO_FIELDS.MIF_LOADADDRESS))
            {
                info.m_addrLoadAddress = _lldbModule.GetCodeLoadAddress();
                info.dwValidFields |= enum_MODULE_INFO_FIELDS.MIF_LOADADDRESS;
            }

            if (HasFlag(enum_MODULE_INFO_FIELDS.MIF_PREFFEREDADDRESS))
            {
                // TODO: Find the actual preferred load address rather than
                // pretending the module is loaded in the right place.
                // We may choose to ignore this, as samples do: extracting the preferred base
                // address from the library / executable seems nontrivial.
                // If m_addrLoadAddress is a different value, VS will show a warning on the icons
                // in the Modules window.
                info.m_addrPreferredLoadAddress = _lldbModule.GetCodeLoadAddress();
                info.dwValidFields |= enum_MODULE_INFO_FIELDS.MIF_PREFFEREDADDRESS;
            }
            
            // is used to calculate address's range end
            if (HasFlag(enum_MODULE_INFO_FIELDS.MIF_SIZE))
            {
                info.m_dwSize = (uint)_lldbModule.GetCodeSize();
                info.dwValidFields |= enum_MODULE_INFO_FIELDS.MIF_SIZE;
            }

            // Order
            if (HasFlag(enum_MODULE_INFO_FIELDS.MIF_LOADORDER))
            {
                info.m_dwLoadOrder = _loadOrder;
                info.dwValidFields |= enum_MODULE_INFO_FIELDS.MIF_LOADORDER;
            }

            // SymbolFile
            if (HasFlag(enum_MODULE_INFO_FIELDS.MIF_URLSYMBOLLOCATION))
            {
                SbFileSpec symbolFileSpec = _lldbModule.GetSymbolFileSpec();
                if (symbolFileSpec != null)
                {
                    info.m_bstrUrlSymbolLocation = Path.Combine(
                        symbolFileSpec.GetDirectory(), symbolFileSpec.GetFilename());
                    info.dwValidFields |= enum_MODULE_INFO_FIELDS.MIF_URLSYMBOLLOCATION;
                }
            }

            if (HasFlag(enum_MODULE_INFO_FIELDS.MIF_FLAGS))
            {
                if (_lldbModule.HasSymbolsLoaded())
                {
                    info.m_dwModuleFlags |= enum_MODULE_FLAGS.MODULE_FLAG_SYMBOLS;
                }

                if (_lldbModule.Is64Bit())
                {
                    info.m_dwModuleFlags |= enum_MODULE_FLAGS.MODULE_FLAG_64BIT;
                }

                info.dwValidFields |= enum_MODULE_INFO_FIELDS.MIF_FLAGS;
            }

            moduleInfo[0] = info;
            return VSConstants.S_OK;

            bool HasFlag(enum_MODULE_INFO_FIELDS flag) => (flag & fields) != 0;
        }

        public int ReloadSymbols_Deprecated(string urlToSymbols, out string debugMessage)
        {
            debugMessage = null;
            return VSConstants.E_NOTIMPL;
        }

        #endregion

        #region IDebugModule3 functions

        public int GetSymbolInfo(enum_SYMBOL_SEARCH_INFO_FIELDS fields,
                                 MODULE_SYMBOL_SEARCH_INFO[] searchInfo)
        {
            var info = new MODULE_SYMBOL_SEARCH_INFO();

            if ((enum_SYMBOL_SEARCH_INFO_FIELDS.SSIF_VERBOSE_SEARCH_INFO & fields) != 0)
            {
                string log = _moduleSearchLogHolder.GetSearchLog(_lldbModule);
                if (string.IsNullOrEmpty(log))
                {
                    if (_lldbModule.HasSymbolsLoaded())
                    {
                        log = "Symbols for this module were automatically located by LLDB.";
                    }
                }

                info.bstrVerboseSearchInfo = log;
                info.dwValidFields |=
                    (uint)enum_SYMBOL_SEARCH_INFO_FIELDS.SSIF_VERBOSE_SEARCH_INFO;
            }

            searchInfo[0] = info;
            return VSConstants.S_OK;
        }

        public int LoadSymbols()
        {
            IAction action = _actionRecorder.CreateToolAction(ActionType.DebugModuleLoadSymbols);

            ICancelableTask<LoadModuleFilesResult> loadSymbolsTask = _cancelableTaskFactory.Create(
                "Loading symbols...",
                task => _moduleFileLoader.LoadModuleFilesAsync(new[] { _lldbModule }, task,
                                                          _moduleFileLoadRecorderFactory.Create(
                                                              action)));

            if (!loadSymbolsTask.RunAndRecord(action))
            {
                return VSConstants.E_ABORT;
            }

            _engineHandler.OnSymbolsLoaded(Self, ModuleName, null,
                                           loadSymbolsTask.Result.ResultCode == VSConstants.S_OK, _program);

            // Returning E_FAIL causes Visual Studio to show a file dialog when attached
            // to a running program or crash dump. This dialog can only be used to select PDB
            // files.
            return loadSymbolsTask.Result.ResultCode == VSConstants.E_FAIL
                ? VSConstants.S_OK
                : loadSymbolsTask.Result.ResultCode;
        }

        public int IsUserCode(out int pfUser)
        {
            pfUser = 1; // TRUE
            return VSConstants.E_NOTIMPL;
        }

        public int SetJustMyCodeState(int fIsUserCode) => VSConstants.E_NOTIMPL;

        #endregion
    }
}
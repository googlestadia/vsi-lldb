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
            readonly ILldbModuleUtil _moduleUtil;
            readonly ISymbolSettingsProvider _symbolSettingsProvider;

            [Obsolete("This constructor only exists to support mocking libraries.", true)]
            protected Factory()
            {
            }

            public Factory(CancelableTask.Factory cancelableTaskFactory,
                           ActionRecorder actionRecorder,
                           ModuleFileLoadMetricsRecorder.Factory moduleFileLoadRecorderFactory,
                           ILldbModuleUtil moduleUtil,
                           ISymbolSettingsProvider symbolSettingsProvider)
            {
                _cancelableTaskFactory = cancelableTaskFactory;
                _actionRecorder = actionRecorder;
                _moduleUtil = moduleUtil;
                _symbolSettingsProvider = symbolSettingsProvider;
                _moduleFileLoadRecorderFactory = moduleFileLoadRecorderFactory;
            }

            public virtual IDebugModule3 Create(
                IModuleFileLoader moduleFileLoader, IModuleSearchLogHolder moduleSearchLogHolder,
                SbModule lldbModule, uint loadOrder, IDebugEngineHandler debugEngineHandler,
                IGgpDebugProgram program) => new DebugModule(_cancelableTaskFactory,
                                                             _actionRecorder,
                                                             _moduleFileLoadRecorderFactory,
                                                             _moduleUtil, moduleFileLoader,
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
        readonly ILldbModuleUtil _moduleUtil;
        readonly IGgpDebugProgram _program;
        readonly ISymbolSettingsProvider _symbolSettingsProvider;

        string ModuleName => _lldbModule.GetPlatformFileSpec()?.GetFilename() ?? "<unknown>";

        DebugModule(CancelableTask.Factory cancelableTaskFactory, ActionRecorder actionRecorder,
                    ModuleFileLoadMetricsRecorder.Factory moduleFileLoadRecorderFactory,
                    ILldbModuleUtil moduleUtil, IModuleFileLoader moduleFileLoader,
                    IModuleSearchLogHolder moduleSearchLogHolder, SbModule lldbModule,
                    uint loadOrder, IDebugEngineHandler engineHandler, IGgpDebugProgram program,
                    ISymbolSettingsProvider symbolSettingsProvider)
        {
            _cancelableTaskFactory = cancelableTaskFactory;
            _actionRecorder = actionRecorder;
            _moduleFileLoadRecorderFactory = moduleFileLoadRecorderFactory;
            _moduleUtil = moduleUtil;
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

            if ((enum_MODULE_INFO_FIELDS.MIF_NAME & fields) != 0)
            {
                if (platformFileSpec != null)
                {
                    info.m_bstrName = platformFileSpec.GetFilename();
                    info.dwValidFields |= enum_MODULE_INFO_FIELDS.MIF_NAME;
                }
            }

            // "URL" fills in the "Path" column in the Modules window.
            if ((enum_MODULE_INFO_FIELDS.MIF_URL & fields) != 0)
            {
                // The module paths are for remote files (on Linux).
                if (platformFileSpec != null)
                {
                    info.m_bstrUrl = FileUtil.PathCombineLinux(
                        platformFileSpec.GetDirectory(), platformFileSpec.GetFilename());
                }

                info.dwValidFields |= enum_MODULE_INFO_FIELDS.MIF_URL;
            }

            // "URLSYMBOLLOCATION" fills in the Symbol File Location column.
            if ((enum_MODULE_INFO_FIELDS.MIF_URLSYMBOLLOCATION & fields) != 0)
            {
                if (_moduleUtil.HasSymbolsLoaded(_lldbModule))
                {
                    // The symbol paths are for local files (on Windows).
                    SbFileSpec symbolFileSpec = _lldbModule.GetSymbolFileSpec();
                    if (symbolFileSpec != null)
                    {
                        info.m_bstrUrlSymbolLocation = Path.Combine(
                            symbolFileSpec.GetDirectory(), symbolFileSpec.GetFilename());
                        info.dwValidFields |= enum_MODULE_INFO_FIELDS.MIF_URLSYMBOLLOCATION;
                    }
                }
            }

            if ((enum_MODULE_INFO_FIELDS.MIF_LOADADDRESS & fields) != 0)
            {
                info.m_addrLoadAddress = _lldbModule.GetCodeLoadAddress();
                info.dwValidFields |= enum_MODULE_INFO_FIELDS.MIF_LOADADDRESS;
            }

            if ((enum_MODULE_INFO_FIELDS.MIF_PREFFEREDADDRESS & fields) != 0)
            {
                // TODO: Find the actual preferred load address rather than
                // pretending the module is loaded in the right place.
                // We may choose to ignore this, as samples do: extracting the preferred base
                // address from the library / executable seems nontrivial.
                // If m_addrLoadAddress is a different value, VS will show a warning on the icons
                // in the Modules window.
                info.m_addrPreferredLoadAddress = info.m_addrLoadAddress;
                info.dwValidFields |= enum_MODULE_INFO_FIELDS.MIF_PREFFEREDADDRESS;
            }

            if ((enum_MODULE_INFO_FIELDS.MIF_SIZE & fields) != 0)
            {
                info.m_dwSize = (uint) _lldbModule.GetCodeSize();
                info.dwValidFields |= enum_MODULE_INFO_FIELDS.MIF_SIZE;
            }

            if ((enum_MODULE_INFO_FIELDS.MIF_LOADORDER & fields) != 0)
            {
                info.m_dwLoadOrder = _loadOrder;
                info.dwValidFields |= enum_MODULE_INFO_FIELDS.MIF_LOADORDER;
            }

            if ((enum_MODULE_INFO_FIELDS.MIF_FLAGS & fields) != 0)
            {
                info.m_dwModuleFlags = 0;
                if (_moduleUtil.HasSymbolsLoaded(_lldbModule))
                {
                    info.m_dwModuleFlags |= enum_MODULE_FLAGS.MODULE_FLAG_SYMBOLS;
                }

                if (_lldbModule.Is64Bit())
                {
                    info.m_dwModuleFlags |= enum_MODULE_FLAGS.MODULE_FLAG_64BIT;
                }

                info.dwValidFields |= enum_MODULE_INFO_FIELDS.MIF_FLAGS;
            }

            if ((enum_MODULE_INFO_FIELDS.MIF_DEBUGMESSAGE & fields) != 0)
            {
                if (!_moduleUtil.HasSymbolsLoaded(_lldbModule))
                {
                    var inclusionSetting = _symbolSettingsProvider.GetInclusionSettings();
                    if (!inclusionSetting.IsModuleIncluded(ModuleName))
                    {
                        info.m_bstrDebugMessage = SymbolInclusionSettings.ModuleExcludedMessage;
                    }
                }

                info.dwValidFields |= enum_MODULE_INFO_FIELDS.MIF_DEBUGMESSAGE;
            }

            moduleInfo[0] = info;
            return VSConstants.S_OK;
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
                    if (_moduleUtil.HasSymbolsLoaded(_lldbModule))
                    {
                        log = "Symbols for this module were automatically located by LLDB.";
                    }
                    else if (!_symbolSettingsProvider.IsSymbolServerEnabled)
                    {
                        log = "Symbols are not loaded automatically because symbol server " +
                            "support is disabled." + Environment.NewLine +
                            "Please enable symbol server support in Stadia SDK settings " +
                            "to load symbols automatically when the debug session is started." +
                            Environment.NewLine +
                            "Note that this may slow down your debug session startup.";
                    }
                }

                info.bstrVerboseSearchInfo = log;
                info.dwValidFields |=
                    (uint) enum_SYMBOL_SEARCH_INFO_FIELDS.SSIF_VERBOSE_SEARCH_INFO;
            }

            searchInfo[0] = info;
            return VSConstants.S_OK;
        }

        public int LoadSymbols()
        {
            IAction action = _actionRecorder.CreateToolAction(ActionType.DebugModuleLoadSymbols);

            ICancelableTask<int> loadSymbolsTask = _cancelableTaskFactory.Create(
                "Loading symbols...",
                task => _moduleFileLoader.LoadModuleFiles(new[] {_lldbModule}, task,
                                                          _moduleFileLoadRecorderFactory.Create(
                                                              action)));

            if (!loadSymbolsTask.RunAndRecord(action))
            {
                return VSConstants.E_ABORT;
            }

            _engineHandler.OnSymbolsLoaded(Self, ModuleName, null,
                                           loadSymbolsTask.Result == VSConstants.S_OK, _program);
            // Returning E_FAIL causes Visual Studio to show a file dialog when attached
            // to a running program or crash dump. This dialog can only be used to select PDB
            // files.
            return loadSymbolsTask.Result == VSConstants.E_FAIL
                ? VSConstants.S_OK
                : loadSymbolsTask.Result;
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
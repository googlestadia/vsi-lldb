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
using Microsoft.VisualStudio.Debugger.Interop;

namespace YetiVSITestsCommon.Services
{
    public class DebugOptionListStub : IDebugOptionList120A
    {
        public void Add(string Name, bool IsEnabled) =>
            throw new NotImplementedException();

        public void Remove(string Name) =>
            throw new NotImplementedException();

        public void Clear() =>
            throw new NotImplementedException();

        public int Find(int lStartIndex, string Name, bool useWildcard) =>
            throw new NotImplementedException();

        public IDebugOptionList120A Clone() =>
            throw new NotImplementedException();

        public int Count => 0;

        public DebugOption this[int lIndex] => throw new NotImplementedException();
    }

    public class VsDebuggerSymbolSettingsStub : IVsDebuggerSymbolSettings120A
    {
        DebugOptionListStub _symbolPaths => new DebugOptionListStub();
        DebugOptionListStub _includeList => new DebugOptionListStub();
        DebugOptionListStub _excldueList => new DebugOptionListStub();

        public IVsDebuggerSymbolSettings120A Clone()
        {
            throw new NotImplementedException();
        }

        public IDebugOptionList120A SymbolPaths => _symbolPaths;

        public IDebugOptionList120A IncludeList => _includeList;

        public IDebugOptionList120A ExcludeList => _excldueList;

        public string CachePath
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public bool IsManualLoad
        {
            get => false;
            set => throw new NotImplementedException();
        }

        public bool IsLoadAdjacent
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public bool UseMSSymbolServers
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }
    }

    public class VsDebuggerSymbolSettingsManagerStub : IVsDebuggerSymbolSettingsManager120A
    {
        readonly VsDebuggerSymbolSettingsStub _settings = new VsDebuggerSymbolSettingsStub();

        public IVsDebuggerSymbolSettings120A GetCurrentSymbolSettings() => _settings;

        public void SaveSymbolSettings(IVsDebuggerSymbolSettings120A pSymbolSettings,
                                       bool loadSymbolsNow = false)
        {
            throw new NotImplementedException();
        }
    }
}
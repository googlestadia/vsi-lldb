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

using System.ComponentModel;
using System.IO;
using YetiCommon;
using YetiVSI.DebugEngine.CoreDumps;

namespace YetiVSI.DebugEngine.CoreDumpViewer
{
    public sealed class ModuleNode : INotifyPropertyChanged
    {
        public ModuleNode(DumpModule dumpModule)
        {
            BuildId = dumpModule.BuildId;
            IsExecutable = dumpModule.IsExecutable;

            ModuleGroup = dumpModule.Path;
            ModuleVersion = "0.0.0.0";
            ModuleName = Path.GetFileName(dumpModule.Path);
        }

        public bool IsExecutable { get; }
        public BuildId BuildId { get; }

        string _moduleName;
        public string ModuleName
        {
            get => _moduleName;
            set
            {
                _moduleName = value;
                OnPropertyChanged(nameof(ModuleName));
            }
        }

        string _moduleVersion;
        public string ModuleVersion
        {
            get => _moduleVersion;
            set
            {
                _moduleVersion = value;
                OnPropertyChanged(nameof(ModuleVersion));
            }
        }

        string _moduleGroup;
        public string ModuleGroup
        {
            get => _moduleGroup;
            set
            {
                _moduleGroup = value;
                OnPropertyChanged(nameof(ModuleGroup));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
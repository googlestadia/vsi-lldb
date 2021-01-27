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

﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Text;
using System.Windows;
using System.Windows.Data;
using YetiVSI.DebugEngine.CoreDumps;

namespace YetiVSI.DebugEngine.CoreDumpViewer
{
    public class ViewModel : INotifyPropertyChanged
    {
        string _fullDescription;
        List<ModuleNode> _modules;
        public ViewModel(string filename)
        {
            FileName = $"{Path.GetFileName(filename)} : {filename}";
            FullPathFileName = filename;
            TimeStamp = File.GetLastWriteTime(filename);
            try
            {
                ProcessModules(filename);
            }
            catch (SystemException exception)
            {
                Trace.WriteLine($"Couldn't parse modules in core file {filename}\n" +
                                $"Exception: {exception}");
                MessageBox.Show("Error", ErrorStrings.CorruptedCoreFile,
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public ICollectionView Modules { get; private set; }

        public string GetFullDescription()
        {
            if (_fullDescription == null)
            {
                var builder = new StringBuilder();
                builder.Append("Core Dump Summary\n----------------\n");
                builder.AppendLine($"Dump File: {FileName}");
                builder.AppendLine($"Last Write Time: {TimeStamp}");
                builder.AppendLine($"Process Name: {ProgramNameValue}");
                builder.Append("\n----------------\nModules\n----------------\n" +
                               "Module Name\tModule Path\tModule Version\n----------------\n");

                foreach (ModuleNode module in _modules)
                {
                    builder.AppendLine(
                        $"{module.ModuleName}\t{module.ModuleGroup}\t{module.ModuleVersion}");
                }

                _fullDescription = builder.ToString();
            }
            return _fullDescription;
        }

        void ProcessModules(string filename)
        {
            if (_modules == null)
            {
                _modules = new List<ModuleNode>();
                var moduleProvider = new DumpModulesProvider(new FileSystem());
                DumpReadResult dumpModules = moduleProvider.GetModules(filename);

                foreach (DumpModule dumpModule in dumpModules.Modules)
                {
                    var module = new ModuleNode(dumpModule);
                    if (module.IsExecutable)
                    {
                        ProgramNameValue = $"{module.ModuleName} : {module.ModuleGroup}";
                    }

                    _modules.Add(module);
                }
            }

            Modules = CollectionViewSource.GetDefaultView(_modules);
        }

        string _fileName;
        public string FileName
        {
            get => _fileName;
            set
            {
                _fileName = value;
                OnPropertyChanged(nameof(FileName));
            }
        }

        string _fullPathFileName;
        public string FullPathFileName
        {
            get => _fullPathFileName;
            set
            {
                _fullPathFileName = value;
                OnPropertyChanged(nameof(FullPathFileName));
            }
        }

        DateTime _timeStamp;
        public DateTime TimeStamp
        {
            get => _timeStamp;
            set
            {
                _timeStamp = value;
                OnPropertyChanged(nameof(TimeStamp));
            }
        }

        string _programNameValue;
        public string ProgramNameValue
        {
            get => _programNameValue;
            set
            {
                _programNameValue = value;
                OnPropertyChanged(nameof(ProgramNameValue));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
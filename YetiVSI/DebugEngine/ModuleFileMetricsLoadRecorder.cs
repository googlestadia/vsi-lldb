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
using System.Collections.Generic;
using System.Linq;
using DebuggerApi;
using YetiVSI.Shared.Metrics;
using static YetiVSI.Shared.Metrics.DeveloperLogEvent.Types;

namespace YetiVSI.DebugEngine
{
    /// <summary>
    /// Helper class to record metrics related to the loading of symbols and binaries.
    /// </summary>
    public interface IModuleFileLoadMetricsRecorder
    {
        /// <summary>
        /// Records metrics about how many modules have symbols and binaries before the event.
        /// This method should be called at the beginning of an event where symbols and binaries
        /// may be loaded.
        /// </summary>
        void RecordBeforeLoad(IList<SbModule> modules);

        /// <summary>
        /// Records metrics about how many modules have symbols and binaries before the event.
        /// This method should be called at the beginning of an event where symbols and binaries
        /// may be loaded.
        /// </summary>
        void RecordBeforeLoad(LoadSymbolData loadSymbolData);

        /// <summary>
        /// Records metrics about how many modules have symbols and binaries before the event.
        /// This method should be called at the beginning of an event where symbols and binaries
        /// may be loaded.
        /// </summary>
        void RecordAfterLoad(IList<SbModule> modules);

        /// <summary>
        /// Records metrics about how many modules have symbols and binaries after the event.
        /// This method should be called at the end of an event where symbols and binaries may be
        /// loaded.
        /// </summary>
        void RecordAfterLoad(LoadSymbolData loadSymbolData);
    }

    public class ModuleFileLoadMetricsRecorder : IModuleFileLoadMetricsRecorder
    {
        public class Factory
        {
            readonly IModuleFileFinder _moduleFileFinder;

            [Obsolete("This constructor only exists to support mocking libraries.", error: true)]
            protected Factory() { }

            public Factory(IModuleFileFinder moduleFileFinder)
            {
                _moduleFileFinder = moduleFileFinder;
            }

            public virtual IModuleFileLoadMetricsRecorder Create(Metrics.IAction action)
            {
                return new ModuleFileLoadMetricsRecorder(_moduleFileFinder, action);
            }
        }

        readonly IModuleFileFinder _moduleFileFinder;
        readonly Metrics.IAction _action;

        public ModuleFileLoadMetricsRecorder(
            IModuleFileFinder moduleFileFinder, Metrics.IAction action)
        {
            _moduleFileFinder = moduleFileFinder;
            _action = action;
        }

        public void RecordBeforeLoad(IList<SbModule> modules)
        {
            var loadSymbolDataBuilder = new LoadSymbolData
            {
                ModulesBeforeCount = modules.Count,
                ModulesWithSymbolsLoadedBeforeCount = modules.Count(m => m.HasSymbolsLoaded()),
                BinariesLoadedBeforeCount = modules.Count(m => m.HasBinaryLoaded())
            };

            RecordBeforeLoad(loadSymbolDataBuilder);
        }

        public void RecordBeforeLoad(LoadSymbolData loadSymbolData)
        {
            // Record SymbolStore collection information.
            _moduleFileFinder.RecordMetrics(loadSymbolData);
            _action.UpdateEvent(new DeveloperLogEvent
            {
                LoadSymbolData = loadSymbolData
            });
        }

        public void RecordAfterLoad(IList<SbModule> modules)
        {
            var loadSymbolDataBuilder = new LoadSymbolData
            {
                // Update module count again if more modules were loaded during the event.
                ModulesAfterCount = modules.Count,
                ModulesWithSymbolsLoadedAfterCount = modules.Count(m => m.HasSymbolsLoaded()),
                BinariesLoadedAfterCount = modules.Count(m => m.HasBinaryLoaded())
            };

            RecordAfterLoad(loadSymbolDataBuilder);
        }

        public void RecordAfterLoad(LoadSymbolData loadSymbolData)
        {
            _action.UpdateEvent(new DeveloperLogEvent
            {
                LoadSymbolData = loadSymbolData
            });
        }
    }
}

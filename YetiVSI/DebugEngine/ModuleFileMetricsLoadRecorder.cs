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

﻿using DebuggerApi;
using System;
using System.Collections.Generic;
using System.Linq;
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
        /// Records metrics about how many modules have symbols and binaries after the event.
        /// This method should be called at the end of an event where symbols and binaries may be
        /// loaded.
        /// </summary>
        void RecordAfterLoad(IList<SbModule> modules);

        /// <summary>
        /// Records that the relevant feature implementing this action is disabled.
        /// </summary>
        void RecordFeatureDisabled();
    }

    public class ModuleFileLoadMetricsRecorder : IModuleFileLoadMetricsRecorder
    {
        public class Factory
        {
            ILldbModuleUtil moduleUtil;
            IModuleFileFinder moduleFileFinder;

            [Obsolete("This constructor only exists to support mocking libraries.", error: true)]
            protected Factory() { }

            public Factory(ILldbModuleUtil moduleUtil, IModuleFileFinder moduleFileFinder)
            {
                this.moduleUtil = moduleUtil;
                this.moduleFileFinder = moduleFileFinder;
            }

            public virtual IModuleFileLoadMetricsRecorder Create(Metrics.IAction action)
            {
                return new ModuleFileLoadMetricsRecorder(moduleUtil, moduleFileFinder, action);
            }
        }

        ILldbModuleUtil moduleUtil;
        IModuleFileFinder moduleFileFinder;
        Metrics.IAction action;

        public ModuleFileLoadMetricsRecorder(ILldbModuleUtil moduleUtil,
            IModuleFileFinder moduleFileFinder, Metrics.IAction action)
        {
            this.moduleUtil = moduleUtil;
            this.moduleFileFinder = moduleFileFinder;
            this.action = action;
        }

        public void RecordBeforeLoad(IList<SbModule> modules)
        {
            var loadSymbolDataBuilder = new LoadSymbolData();
            moduleFileFinder.RecordMetrics(loadSymbolDataBuilder);
            loadSymbolDataBuilder.ModulesBeforeCount = modules.Count;
            loadSymbolDataBuilder.ModulesWithSymbolsLoadedBeforeCount =
                modules.Count(m => moduleUtil.HasSymbolsLoaded(m));
            loadSymbolDataBuilder.BinariesLoadedBeforeCount =
                modules.Count(m => moduleUtil.HasBinaryLoaded(m));
            action.UpdateEvent(new DeveloperLogEvent
            {
                LoadSymbolData = loadSymbolDataBuilder
            });
        }

        public void RecordAfterLoad(IList<SbModule> modules)
        {
            var loadSymbolDataBuilder = new LoadSymbolData();
            // Update module count again if more modules were loaded during the event.
            loadSymbolDataBuilder.ModulesAfterCount =
                Math.Max(modules.Count, loadSymbolDataBuilder.ModulesCount.GetValueOrDefault());
            loadSymbolDataBuilder.ModulesWithSymbolsLoadedAfterCount =
                modules.Count(m => moduleUtil.HasSymbolsLoaded(m));
            loadSymbolDataBuilder.BinariesLoadedAfterCount =
                modules.Count(m => moduleUtil.HasBinaryLoaded(m));
            action.UpdateEvent(new DeveloperLogEvent {LoadSymbolData = loadSymbolDataBuilder});
        }

        public void RecordFeatureDisabled()
        {
            action.UpdateEvent(new DeveloperLogEvent
            {
                StatusCode = DeveloperEventStatus.Types.Code.FeatureDisabled
            });
        }
    }
}

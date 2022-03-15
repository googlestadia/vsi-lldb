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

using Metrics.Shared;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using YetiCommon;
using Metrics;
using YetiVSITestsCommon;
using YetiVSITestsCommon.Services;

namespace YetiVSI.Test.MediumTestsSupport
{
    public class MediumTestServiceManager : FakeServiceManager
    {
        public MediumTestServiceManager(JoinableTaskContext taskContext) : this(
            taskContext, OptionPageGrid.CreateForTesting())
        {
        }

        public MediumTestServiceManager(JoinableTaskContext taskContext, OptionPageGrid vsiOptions)
            : base(taskContext)
        {
            var vsiService = new YetiVSIService(vsiOptions);
            var symbolSettingsManager = new VsDebuggerSymbolSettingsManagerStub();
            var metrics = new MetricsService(taskContext, Versions.Populate(null));

            AddService(typeof(SVsShellDebugger), symbolSettingsManager);
            AddService(typeof(YetiVSIService), vsiService);
            AddService(typeof(SMetrics), metrics);
        }
    }
}
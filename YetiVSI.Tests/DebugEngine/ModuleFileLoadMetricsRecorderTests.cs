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
using NSubstitute;
using NUnit.Framework;
using YetiVSI.DebugEngine;
using YetiVSI.Metrics;
using YetiVSI.Shared.Metrics;
using static YetiVSI.Shared.Metrics.DeveloperLogEvent.Types;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class ModuleFileLoadMetricsRecorderTests
    {
        ILldbModuleUtil mockModuleUtil;
        IModuleFileFinder mockModuleFileFinder;
        ModuleFileLoadMetricsRecorder moduleFileLoadRecorder;
        Action action;

        [SetUp]
        public void SetUp()
        {
            mockModuleUtil = Substitute.For<ILldbModuleUtil>();
            mockModuleFileFinder = Substitute.For<IModuleFileFinder>();
            action = new Action(
                DeveloperEventType.Types.Type.VsiDebugEngineLoadSymbols,
                Substitute.For<Timer.Factory>(), Substitute.For<IMetrics>());
            moduleFileLoadRecorder = new ModuleFileLoadMetricsRecorder(mockModuleUtil,
                mockModuleFileFinder, action);
        }

        [Test]
        public void RecordBeforeLoad()
        {
            mockModuleFileFinder.When(x => x.RecordMetrics(Arg.Any<LoadSymbolData>()))
                .Do((x) =>
                {
                    var data = x.Arg<LoadSymbolData>();
                    data.StadiaSymbolStoresCount = 7;
                    data.FlatSymbolStoresCount = 6;
                    data.StructuredSymbolStoresCount = 5;
                    data.HttpSymbolStoresCount = 4;
                });
            var modules = new[]
            {
                CreateMockModule(binaryLoaded: false, symbolsLoaded: false),
                CreateMockModule(binaryLoaded: true, symbolsLoaded: false),
                CreateMockModule(binaryLoaded: true, symbolsLoaded: true),
            };

            moduleFileLoadRecorder.RecordBeforeLoad(modules);

            var loadSymbolData = action.GetEvent().LoadSymbolData;
            Assert.Multiple(() =>
            {
                Assert.AreEqual(loadSymbolData.StadiaSymbolStoresCount, 7);
                Assert.AreEqual(loadSymbolData.FlatSymbolStoresCount, 6);
                Assert.AreEqual(loadSymbolData.StructuredSymbolStoresCount, 5);
                Assert.AreEqual(loadSymbolData.HttpSymbolStoresCount, 4);
                Assert.AreEqual(loadSymbolData.ModulesBeforeCount, 3);
                Assert.AreEqual(loadSymbolData.BinariesLoadedBeforeCount, 2);
                Assert.AreEqual(loadSymbolData.ModulesWithSymbolsLoadedBeforeCount, 1);
            });
        }

        [Test]
        public void RecordAfterLoad()
        {
            var modules = new[]
            {
                CreateMockModule(false, false),
                CreateMockModule(true, false),
                CreateMockModule(true, true),
            };

            moduleFileLoadRecorder.RecordAfterLoad(modules);

            var loadSymbolData = action.GetEvent().LoadSymbolData;
            Assert.Multiple(() =>
            {
                Assert.AreEqual(loadSymbolData.ModulesAfterCount, 3);
                Assert.AreEqual(loadSymbolData.BinariesLoadedAfterCount, 2);
                Assert.AreEqual(loadSymbolData.ModulesWithSymbolsLoadedAfterCount, 1);
            });
        }

        /// <summary>
        /// Creates a new mock module, and configures the values that mockModuleUtil returns in
        /// regards to said module.
        /// </summary>
        SbModule CreateMockModule(bool binaryLoaded, bool symbolsLoaded)
        {
            var module = Substitute.For<SbModule>();
            mockModuleUtil.HasSymbolsLoaded(module).Returns(symbolsLoaded);
            mockModuleUtil.HasBinaryLoaded(module).Returns(binaryLoaded);
            return module;
        }
    }
}

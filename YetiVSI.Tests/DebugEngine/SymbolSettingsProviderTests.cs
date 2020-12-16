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
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NSubstitute;
using NUnit.Framework;
using YetiVSI.DebugEngine;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class SymbolSettingsProviderTests
    {
        [Test]
        public void DisabledInclusionSettingsAreFiltered()
        {
            var settingsManager = Substitute.For<IVsDebuggerSymbolSettingsManager120A>();
            var debuggerService = Substitute.For<IVsDebugger2>();
            var taskContext = new JoinableTaskContext();

            var settings = Substitute.For<IVsDebuggerSymbolSettings120A>();
            settingsManager.GetCurrentSymbolSettings().Returns(settings);

            var options = new DebugOptionList120AStub();
            options.Add("enabled", true);
            options.Add("disabled", false);

            settings.IncludeList.Returns(options);
            settings.ExcludeList.Returns(options);

            SymbolInclusionSettings inclusionSettings =
                new SymbolSettingsProvider(settingsManager, debuggerService, false, taskContext)
                    .GetInclusionSettings();
            Assert.That(inclusionSettings.IncludeList, Does.Contain("enabled"));
            Assert.That(inclusionSettings.IncludeList, Does.Not.Contain("disabled"));
            Assert.That(inclusionSettings.ExcludeList, Does.Contain("enabled"));
            Assert.That(inclusionSettings.ExcludeList, Does.Not.Contain("disabled"));
        }

        [Test]
        public void ModuleInclusionCheckReturnsTrueWhenInIncludedList()
        {
            bool useIncludeList = true;
            var settings = new SymbolInclusionSettings(useIncludeList, new List<string>(),
                                                       new List<string>() { "includedModule" });

            Assert.That(settings.IsModuleIncluded("includedModule"), Is.True);
            Assert.That(settings.IsModuleIncluded("otherModule"), Is.False);
        }

        [Test]
        public void ModuleInclusionCheckReturnsTrueWhenNotInExcludedList()
        {
            bool useIncludeList = false;
            var settings = new SymbolInclusionSettings(
                useIncludeList, new List<string>() { "excludedModule" }, new List<string>());

            Assert.That(settings.IsModuleIncluded("someModule"), Is.True);
            Assert.That(settings.IsModuleIncluded("excludedModule"), Is.False);
        }

        [Test]
        public void ModuleInclusionCheckIsCaseInsensitive()
        {
            bool useIncludeList = true;
            var settings =
                new SymbolInclusionSettings(useIncludeList, new List<string>(),
                                            new List<string>() { "includedModule", "oneMore" });

            Assert.That(settings.IsModuleIncluded("INCLUDEDMODULE"), Is.True);
            Assert.That(settings.IsModuleIncluded("OnEmOrE"), Is.True);
        }

        [Test]
        public void ModuleInclusionCheckAcceptsRegex()
        {
            bool useIncludeList = true;
            var settings = new SymbolInclusionSettings(useIncludeList, new List<string>(),
                                                       new List<string>()
                                                           { "excluded*", "m?dule" });

            Assert.That(settings.IsModuleIncluded("excludedModule"), Is.True);
            Assert.That(settings.IsModuleIncluded("excluded"), Is.True);
            Assert.That(settings.IsModuleIncluded("excluded.dll"), Is.True);
            Assert.That(settings.IsModuleIncluded("Module"), Is.True);
            Assert.That(settings.IsModuleIncluded("Moodule"), Is.False);
            Assert.That(settings.IsModuleIncluded("SomeModule"), Is.False);
            Assert.That(settings.IsModuleIncluded("somethingexcluded"), Is.False);
        }

        class DebugOptionList120AStub : IDebugOptionList120A
        {
            readonly IList<DebugOption> _options = new List<DebugOption>();

            public void Add(string name, bool isEnabled)
            {
                var option = new DebugOption { Name = name, IsEnabled = isEnabled };
                _options.Add(option);
            }

            public void Remove(string name) => throw new NotImplementedException();

            public void Clear() => throw new NotImplementedException();

            public int Find(int lStartIndex, string name, bool useWildcard) =>
                throw new NotImplementedException();

            public IDebugOptionList120A Clone() => throw new NotImplementedException();

            public int Count => _options.Count;

            public DebugOption this[int lIndex] => _options[lIndex];
        }
    }
}
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
using NUnit.Framework;
using NSubstitute;
using YetiVSI.DebugEngine;
using Microsoft.VisualStudio.Debugger.Interop;
using System;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class DebugModuleCacheTests
    {
        IDebugModuleCache debugModuleCache;
        EventHandler<ModuleAddedEventArgs> moduleAddedHandler;
        EventHandler<ModuleRemovedEventArgs> moduleRemovedHandler;

        [SetUp]
        public void SetUp()
        {
            debugModuleCache = new DebugModuleCache(
                Substitute.For<DebugModuleCache.ModuleCreator>());
            moduleAddedHandler = Substitute.For<EventHandler<ModuleAddedEventArgs>>();
            moduleRemovedHandler = Substitute.For<EventHandler<ModuleRemovedEventArgs>>();
            debugModuleCache.ModuleAdded += moduleAddedHandler;
            debugModuleCache.ModuleRemoved += moduleRemovedHandler;
        }

        [Test]
        public void GetOrCreate_Uncached()
        {
            var sbModuleA = CreateMockSbModule(1);
            var sbModuleB = CreateMockSbModule(2);

            var debugModuleA = debugModuleCache.GetOrCreate(sbModuleA, null);
            var debugModuleB = debugModuleCache.GetOrCreate(sbModuleB, null);

            Assert.AreNotSame(debugModuleA, debugModuleB);
            ReceivedModuleAddedEvent(debugModuleA);
            ReceivedModuleAddedEvent(debugModuleB);
        }

        [Test]
        public void GetOrCreate_Cached()
        {
            var sbModuleA = CreateMockSbModule(1);
            var sbModuleB = CreateMockSbModule(1);

            var debugModuleA = debugModuleCache.GetOrCreate(sbModuleA, null);
            var debugModuleB = debugModuleCache.GetOrCreate(sbModuleB, null);

            Assert.AreSame(debugModuleA, debugModuleB);
            ReceivedModuleAddedEvent(debugModuleA);
        }

        [Test]
        public void TryRemove()
        {
            var sbModule = CreateMockSbModule(1);
            var debugModule = debugModuleCache.GetOrCreate(sbModule, null);

            Assert.True(debugModuleCache.Remove(sbModule));

            ReceivedModuleRemovedEvent(debugModule);
            Assert.AreNotSame(debugModule, debugModuleCache.GetOrCreate(sbModule, null));
        }

        [Test]
        public void RemoveAllExcept()
        {
            var sbModuleA = CreateMockSbModule(1);
            var sbModuleB = CreateMockSbModule(2);
            var debugModuleA = debugModuleCache.GetOrCreate(sbModuleA, null);
            var debugModuleB = debugModuleCache.GetOrCreate(sbModuleB, null);

            debugModuleCache.RemoveAllExcept(new[] { sbModuleA });

            Assert.AreSame(debugModuleA, debugModuleCache.GetOrCreate(sbModuleA, null));
            Assert.AreNotSame(debugModuleB, debugModuleCache.GetOrCreate(sbModuleB, null));
            DidNotReceivedModuleRemovedEvent(debugModuleA);
            ReceivedModuleRemovedEvent(debugModuleB);
        }

        SbModule CreateMockSbModule(int id)
        {
            var sbModule = Substitute.For<SbModule>();
            sbModule.GetId().Returns(id);
            return sbModule;
        }

        void ReceivedModuleAddedEvent(IDebugModule2 debugModule)
        {
            moduleAddedHandler.Received(1).Invoke(debugModuleCache,
                Arg.Is<ModuleAddedEventArgs>(args => args.Module == debugModule));
        }

        void ReceivedModuleRemovedEvent(IDebugModule2 debugModule)
        {
            moduleRemovedHandler.Received(1).Invoke(debugModuleCache,
                Arg.Is<ModuleRemovedEventArgs>(args => args.Module == debugModule));
        }

        void DidNotReceivedModuleRemovedEvent(IDebugModule2 debugModule)
        {
            moduleRemovedHandler.DidNotReceive().Invoke(debugModuleCache,
                Arg.Is<ModuleRemovedEventArgs>(args => args.Module == debugModule));
        }
    }
}

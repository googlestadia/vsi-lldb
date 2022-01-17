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
using Microsoft.VisualStudio.Debugger.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using YetiCommon.CastleAspects;
using YetiVSI.Util;

namespace YetiVSI.DebugEngine
{
    /// <summary>
    /// Caches DebugModules, so that only one instance of DebugModule needs to be created for each
    /// underlying LLDB module.
    /// </summary>
    public interface IDebugModuleCache
    {
        /// <summary>
        /// Raised when a new DebugModule is created and added to the cache. Handlers may still be
        /// invoked for a short period after unsubscribing.
        /// </summary>
        event EventHandler<ModuleAddedEventArgs> ModuleAdded;

        /// <summary>
        /// Raised when an existing DebugModule is removed from the cache. Handlers may still be
        /// invoked for a short period after unsubscribing.
        /// </summary>
        event EventHandler<ModuleRemovedEventArgs> ModuleRemoved;

        /// <summary>
        /// Searches the cache for a DebugModule that matches the given SbModule. If no such
        /// module exists, creates a new DebugModule and returns that.
        /// </summary>
        IDebugModule3 GetOrCreate(SbModule lldbModule, IGgpDebugProgram debugProgram);

        /// <summary>
        /// Removes the DebugModule that matches the given SbModule.
        /// </summary>
        /// <param param name="debugModule">
        /// The module that was removed.
        /// </param>
        /// <returns>
        /// True if a module was removed, false otherwise.
        /// </returns>
        bool Remove(SbModule lldbModule);

        /// <summary>
        /// Removes every DebugModule that does not match one of the given SbModules.
        /// </summary>
        void RemoveAllExcept(IEnumerable<SbModule> liveModules);
    }

    public class ModuleAddedEventArgs : EventArgs
    {
        public IDebugModule2 Module { get; }

        public ModuleAddedEventArgs(IDebugModule2 module) { Module = module; }
    }

    public class ModuleRemovedEventArgs : EventArgs
    {
        public IDebugModule2 Module { get; }

        public ModuleRemovedEventArgs(IDebugModule2 module) { Module = module; }
    }

    /// <summary>
    /// Equality comparer that considers two SbModules to be equal if they represent the same
    /// underlying module in LLDB. Note that the underlying server side code uses a
    /// UniqueObjectStore and assigns the same ID for equal objects, so that testing by ID here is
    /// enough.
    /// </summary>
    public class SbModuleEqualityComparer : IEqualityComparer<SbModule>
    {
        public bool Equals(SbModule x, SbModule y)
        {
            return x.GetId() == y.GetId();
        }

        public int GetHashCode(SbModule module)
        {
            return module.GetId().GetHashCode();
        }

        public static SbModuleEqualityComparer Instance { get; } = new SbModuleEqualityComparer();
    }

    public class DebugModuleCache : SimpleDecoratorSelf<IDebugModuleCache>, IDebugModuleCache
    {
        public delegate IDebugModule3 ModuleCreator(SbModule lldbModule, uint loadOrder,
                                                    IGgpDebugProgram program);

        public class Factory
        {
            readonly IDispatcher mainThreadDispatcher;

            public Factory(IDispatcher mainThreadDispatcher)
            {
                this.mainThreadDispatcher = mainThreadDispatcher;
            }

            public virtual IDebugModuleCache Create(ModuleCreator moduleCreator) =>
                new DebugModuleCache(moduleCreator, SbModuleEqualityComparer.Instance,
                    mainThreadDispatcher);
        }

        public event EventHandler<ModuleAddedEventArgs> ModuleAdded;
        public event EventHandler<ModuleRemovedEventArgs> ModuleRemoved;

        readonly ModuleCreator moduleCreator;
        readonly IEqualityComparer<SbModule> sbModuleEqualityComparer;
        readonly IDispatcher mainThreadDispatcher;

        Dictionary<SbModule, IDebugModule3> cache;
        uint nextLoadOrder = 0;

        DebugModuleCache(ModuleCreator moduleCreator,
                         IEqualityComparer<SbModule> sbModuleEqualityComparer,
                         IDispatcher mainThreadDispatcher)
        {
            this.moduleCreator = moduleCreator;
            this.sbModuleEqualityComparer = sbModuleEqualityComparer;
            this.mainThreadDispatcher = mainThreadDispatcher;
            cache = new Dictionary<SbModule, IDebugModule3>(sbModuleEqualityComparer);
        }

        public IDebugModule3 GetOrCreate(SbModule lldbModule, IGgpDebugProgram program)
        {
            lock (cache)
            {
                if (!cache.TryGetValue(lldbModule, out IDebugModule3 module))
                {
                    module = moduleCreator(lldbModule, nextLoadOrder++, program);
                    cache.Add(lldbModule, module);

                    // TODO: Simplify inter-thread interactions in the VSI, and as part
                    // of that find a less ad-hoc solution to ModuleAdded/Removed event thread
                    // safety issues
                    mainThreadDispatcher.Post(() =>
                    {
                        try
                        {
                            ModuleAdded?.Invoke(Self, new ModuleAddedEventArgs(module));
                        }
                        catch (Exception e)
                        {
                            Trace.WriteLine(
                                $"Warning: ModuleAdded handler failed: {e.Demystify()}");
                        }
                    });
                };
                return module;
            }
        }

        public bool Remove(SbModule lldbModule)
        {
            lock (cache)
            {
                if (cache.TryGetValue(lldbModule, out IDebugModule3 module))
                {
                    cache.Remove(lldbModule);
                    mainThreadDispatcher.Post(() =>
                    {
                        try
                        {
                            ModuleRemoved?.Invoke(Self, new ModuleRemovedEventArgs(module));
                        }
                        catch (Exception e)
                        {
                            Trace.WriteLine(
                                $"Warning: ModuleRemoved handler failed: {e.Demystify()}");
                        }
                    });
                    return true;
                }
                return false;
            }
        }

        public void RemoveAllExcept(IEnumerable<SbModule> liveModules)
        {
            lock (cache)
            {
                var deadModules = cache.Keys.Except(liveModules, sbModuleEqualityComparer).ToList();
                foreach (var module in deadModules) { Remove(module); }
            }
        }
    }
}

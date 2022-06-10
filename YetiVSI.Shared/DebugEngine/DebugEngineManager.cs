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
using System.Collections.Generic;
using System.Diagnostics;

namespace YetiVSI.DebugEngine
{
    /// <summary>
    /// Passed to System.IServiceProvider.GetService(Type) to return a reference to an
    /// IDebugEngineManager.
    ///
    /// Example:
    ///   var debugManager =
    ///     (IDebugEngineManager)serviceProvider.GetGlobalService(typeof(SDebugEngineManager));
    /// </summary>
    public interface SDebugEngineManager { }

    /// <summary>
    /// Tracks the active DebugEngines.
    /// </summary>
    public interface IDebugEngineManager
    {
        void AddDebugEngine(IGgpDebugEngine debugEngine);

        ICollection<IGgpDebugEngine> GetDebugEngines();
    }

    /// <summary>
    /// Tracks DebugEngine's using WeakReferences.
    /// </summary>
    public class DebugEngineManager : IDebugEngineManager
    {
        class DebugEngineEntry
        {
            WeakReference<IGgpDebugEngine> debugEngineRef;

            public DebugEngineEntry(IGgpDebugEngine debugEngine)
            {
                debugEngineRef = new WeakReference<IGgpDebugEngine>(debugEngine, false);
                Id = debugEngine.Id;
            }

            public Guid Id { get; }

            public IGgpDebugEngine DebugEngine
            {
                get
                {
                    IGgpDebugEngine debugEngine;
                    if (!debugEngineRef.TryGetTarget(out debugEngine))
                    {
                        return null;
                    }
                    return debugEngine;
                }
            }
        }

        List<DebugEngineEntry> debugEngines = new List<DebugEngineEntry>();

        public void AddDebugEngine(IGgpDebugEngine debugEngine)
        {
            var entry = new DebugEngineEntry(debugEngine);
            debugEngines.Add(entry);
            debugEngine.SessionEnding += OnSessionEnding;

            Trace.WriteLine($"Added Debug Engine with id={entry.Id}");
        }

        public ICollection<IGgpDebugEngine> GetDebugEngines()
        {
            Purge();
            var activeDebugEngines = new List<IGgpDebugEngine>();
            debugEngines.ForEach(entry =>
            {
                if (entry != null)
                {
                    activeDebugEngines.Add(entry.DebugEngine);
                }
            });
            return activeDebugEngines;
        }

        /// <summary>
        /// Removes all dead IGgpDebugEngines that have been garbage collected.
        /// </summary>
        void Purge()
        {
            List<DebugEngineEntry> nullEntries = debugEngines.FindAll(entry =>
            {
                return entry.DebugEngine == null;
            });
            RemoveEntries(nullEntries);
        }

        /// <summary>
        /// Removes a list of entries.
        /// </summary>
        /// <param name="toRemove">The entries to remove.</param>
        void RemoveEntries(List<DebugEngineEntry> toRemove)
        {
            foreach (var entry in toRemove)
            {
                debugEngines.Remove(entry);
                Trace.WriteLine($"Removed Debug Engine with id={entry.Id}");
            }
        }

        void OnSessionEnding(object sender, EventArgs args)
        {
            ((IGgpDebugEngine)sender).SessionEnding -= OnSessionEnding;

            List<DebugEngineEntry> matchingEntries = debugEngines.FindAll(entry =>
            {
                return entry.DebugEngine == sender;
            });
            RemoveEntries(matchingEntries);
        }
    }
}

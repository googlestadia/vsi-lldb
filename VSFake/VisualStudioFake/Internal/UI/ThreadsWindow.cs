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

using Google.VisualStudioFake.API.UI;
using System;
using System.Collections.Generic;
using Google.VisualStudioFake.API;
using Google.VisualStudioFake.Internal.Jobs;
using Microsoft.VisualStudio.Debugger.Interop;

namespace Google.VisualStudioFake.Internal.UI
{
    public class ThreadsWindow : IThreadsWindow
    {
        readonly IDebugSessionContext _debugSessionContext;
        readonly IJobQueue _jobQueue;

        List<IThread> _threads;

        public ThreadsWindow(IDebugSessionContext debugSessionContext, IJobQueue jobQueue)
        {
            _debugSessionContext = debugSessionContext;
            _jobQueue = jobQueue;
        }

        #region IThreadsWindow

        public void Refresh()
        {
            if (State == ThreadsWindowState.Pending)
            {
                throw new InvalidOperationException("Another refresh operation is still pending.");
            }

            if (_debugSessionContext.DebugProgram == null)
            {
                throw new InvalidOperationException(
                    "There is no program selected; " +
                    $"{nameof(_debugSessionContext.DebugProgram)} is null. " +
                    $"Program state = {_debugSessionContext.ProgramState}.");
            }

            State = ThreadsWindowState.Pending;
            _jobQueue.Push(new GenericJob(UpdateThreads));
        }

        public List<IThread> GetThreads()
        {
            if (State != ThreadsWindowState.Ready)
            {
                throw new InvalidOperationException(
                    $"Threads not available yet. Current threads window state = {State}.");
            }

            return _threads;
        }

        public ThreadsWindowState State { get; private set; }

        public bool Ready => State == ThreadsWindowState.Ready;

        #endregion

        void UpdateThreads()
        {
            HResultChecker.Check(
                _debugSessionContext.DebugProgram.EnumThreads(out IEnumDebugThreads2 enumThreads));
            HResultChecker.Check(enumThreads.GetCount(out uint count));
            uint numFetched = 0;
            var threads = new IDebugThread2[count];
            enumThreads.Next(count, threads, ref numFetched);
            if (numFetched != count)
            {
                throw new InvalidOperationException(
                    $"Failed to fetch threads. Wanted {count}, got {numFetched}.");
            }

            _threads = new List<IThread>();
            foreach (IDebugThread2 t in threads)
            {
                var props = new THREADPROPERTIES[1];
                HResultChecker.Check(
                    t.GetThreadProperties(enum_THREADPROPERTY_FIELDS.TPF_ALLFIELDS, props));
                _threads.Add(new Thread(t, props[0], _debugSessionContext));
            }

            State = ThreadsWindowState.Ready;
        }
    }
}
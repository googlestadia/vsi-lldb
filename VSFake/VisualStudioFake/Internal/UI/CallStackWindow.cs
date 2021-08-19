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

using Google.VisualStudioFake.API;
using Google.VisualStudioFake.API.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using Google.VisualStudioFake.Internal.Jobs;
using Microsoft.VisualStudio.Debugger.Interop;

namespace Google.VisualStudioFake.Internal.UI
{
    public class CallStackWindow : ICallStackWindow
    {
        readonly IDebugSessionContext _debugSessionContext;
        readonly IJobQueue _jobQueue;

        List<IStackFrame> _stackFrames;

        public CallStackWindow(IDebugSessionContext debugSessionContext, IJobQueue jobQueue)
        {
            _debugSessionContext = debugSessionContext;
            _debugSessionContext.SelectedThreadChanged += OnSelectedThreadChanged;
            _jobQueue = jobQueue;
        }

        #region ICallStackWindow

        public List<IStackFrame> GetStackFrames()
        {
            if (State != CallStackWindowState.Ready)
            {
                throw new InvalidOperationException(
                    $"Stack frames not available yet. Current call stack window state = {State}.");
            }

            return _stackFrames;
        }

        public CallStackWindowState State { get; private set; }

        public bool Ready => State == CallStackWindowState.Ready;

        #endregion

        void OnSelectedThreadChanged()
        {
            if (State == CallStackWindowState.Pending)
            {
                throw new InvalidOperationException("Another refresh operation is still pending.");
            }

            IDebugThread2 thread = _debugSessionContext.SelectedThread;
            if (thread == null)
            {
                _stackFrames = new List<IStackFrame>();
                _debugSessionContext.SelectedStackFrame = null;
                return;
            }

            State = CallStackWindowState.Pending;
            _jobQueue.Push(new GenericJob(UpdateFrames));
        }

        void UpdateFrames()
        {
            IDebugThread2 thread = _debugSessionContext.SelectedThread;

            // This is what VS does.
            enum_FRAMEINFO_FLAGS fieldSpec = enum_FRAMEINFO_FLAGS.FIF_FUNCNAME |
                enum_FRAMEINFO_FLAGS.FIF_LANGUAGE | enum_FRAMEINFO_FLAGS.FIF_STACKRANGE |
                enum_FRAMEINFO_FLAGS.FIF_FRAME | enum_FRAMEINFO_FLAGS.FIF_DEBUGINFO |
                enum_FRAMEINFO_FLAGS.FIF_STALECODE | enum_FRAMEINFO_FLAGS.FIF_FLAGS |
                enum_FRAMEINFO_FLAGS.FIF_DEBUG_MODULEP | enum_FRAMEINFO_FLAGS.FIF_FUNCNAME_FORMAT |
                enum_FRAMEINFO_FLAGS.FIF_FUNCNAME_ARGS | enum_FRAMEINFO_FLAGS.FIF_FUNCNAME_MODULE |
                enum_FRAMEINFO_FLAGS.FIF_FUNCNAME_LINES | enum_FRAMEINFO_FLAGS.FIF_FUNCNAME_OFFSET |
                enum_FRAMEINFO_FLAGS.FIF_FUNCNAME_ARGS_TYPES |
                enum_FRAMEINFO_FLAGS.FIF_FUNCNAME_ARGS_NAMES |
                enum_FRAMEINFO_FLAGS.FIF_FILTER_NON_USER_CODE |
                enum_FRAMEINFO_FLAGS.FIF_ARGS_NO_TOSTRING;
            HResultChecker.Check(
                thread.EnumFrameInfo(fieldSpec, 0, out IEnumDebugFrameInfo2 enumFrames));
            HResultChecker.Check(enumFrames.GetCount(out uint count));
            uint numFetched = 0;
            var frames = new FRAMEINFO[count];
            HResultChecker.Check(enumFrames.Next(count, frames, ref numFetched));
            if (numFetched != count)
            {
                throw new InvalidOperationException(
                    $"Failed to fetch frames. Wanted {count}, got {numFetched}.");
            }

            _stackFrames = frames.Select(f => new StackFrame(f, _debugSessionContext))
                .Cast<IStackFrame>().ToList();
            if (_stackFrames.Count > 0)
            {
                _stackFrames[0].Select();
            }

            State = CallStackWindowState.Ready;
        }
    }
}
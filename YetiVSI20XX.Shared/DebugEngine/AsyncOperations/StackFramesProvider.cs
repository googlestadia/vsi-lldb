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
using DebuggerCommonApi;
using DebuggerGrpcClient.Interfaces;
using Microsoft.VisualStudio.Debugger.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace YetiVSI.DebugEngine.AsyncOperations
{
    public class StackFramesProvider
    {
        public delegate IDebugStackFrame StackFrameCreator(RemoteFrame frame, IDebugThread thread,
                                                           IGgpDebugProgram debugProgram);

        public const uint MaxStackDepth = 5000;
        public const uint FramesBatchSize = 200;

        readonly RemoteThread _remoteThread;
        readonly StackFrameCreator _stackFrameCreator;
        readonly IGgpDebugProgram _debugProgram;
        readonly AD7FrameInfoCreator _ad7FrameInfoCreator;
        readonly IGgpDebugProgram _program;

        public StackFramesProvider(RemoteThread remoteThread, StackFrameCreator stackFrameCreator,
                                   IGgpDebugProgram debugProgram,
                                   AD7FrameInfoCreator ad7FrameInfoCreator,
                                   IGgpDebugProgram program)
        {
            _remoteThread = remoteThread;
            _stackFrameCreator = stackFrameCreator;
            _debugProgram = debugProgram;
            _ad7FrameInfoCreator = ad7FrameInfoCreator;
            _program = program;
        }

        public virtual IList<FRAMEINFO> GetRange(enum_FRAMEINFO_FLAGS fieldSpec,
                                                 IDebugThread debugThread, uint startIndex,
                                                 uint count)
        {
            count = (uint) Math.Max(0, Math.Min(count, MaxFramesNumberToLoad - startIndex));
            if (count == 0)
            {
                return new List<FRAMEINFO>();
            }

            List<FrameInfoPair> framesInfo = _remoteThread.GetFramesWithInfo(
                (FrameInfoFlags) fieldSpec, startIndex, count);
            return ToFrameInfo(framesInfo, debugThread, startIndex);
        }

        public virtual async Task<IList<FRAMEINFO>> GetAllAsync(enum_FRAMEINFO_FLAGS fieldSpec,
                                                                IDebugThread debugThread)
        {
            var framesInfo = new List<FrameInfoPair>();
            while (framesInfo.Count < MaxFramesNumberToLoad)
            {
                long maxBatchSize = Math.Min(
                    FramesBatchSize, MaxFramesNumberToLoad - framesInfo.Count);
                List<FrameInfoPair> framesBatch = await _remoteThread.GetFramesWithInfoAsync(
                    (FrameInfoFlags) fieldSpec, (uint) framesInfo.Count, (uint) maxBatchSize);

                if (framesBatch == null)
                {
                    Trace.TraceError("Error on loading Frames With Info. " +
                                     $"Thread Id: {debugThread.GetRemoteThread().GetThreadId()}; " +
                                     $"Start index: {framesInfo.Count}; Max Count: {maxBatchSize}");
                    break;
                }

                framesInfo.AddRange(framesBatch);

                if (framesBatch.Count < maxBatchSize)
                {
                    break;
                }
            }

            return ToFrameInfo(framesInfo, debugThread, 0);
        }

        /// We load one more frame more than we show in order to distinguish the case when
        /// actual count of frames equals <see cref="MaxStackDepth"/> from the case when
        /// number of frames exceeds <see cref="MaxStackDepth"/>.
        public virtual long MaxFramesNumberToLoad => MaxStackDepth + 1;

        List<FRAMEINFO> ToFrameInfo(List<FrameInfoPair> framesInfo, IDebugThread debugThread,
                                    uint startIndex)
        {
            if (framesInfo == null)
            {
                return null;
            }

            var ad7FramesInfo = new List<FRAMEINFO>();
            for (int i = 0; i < framesInfo.Count; ++i)
            {
                var frame = _stackFrameCreator(framesInfo[i].Frame, debugThread, _debugProgram);
                ad7FramesInfo.Add(_ad7FrameInfoCreator.Create(frame, framesInfo[i].Info, _program));
            }

            if (startIndex + ad7FramesInfo.Count > MaxStackDepth)
            {
                int lastIndex = ad7FramesInfo.Count - 1;
                // Create an entry to indicate that the maximum number is exceeded.
                var info = new FrameInfo<SbModule>()
                {
                    FuncName = "The maximum number of stack frames supported by the Stadia " +
                        "debugger has been exceeded.",
                    HasDebugInfo = 0, // for clarity, grays the entry out in the UI
                    ValidFields = FrameInfoFlags.FIF_FUNCNAME | FrameInfoFlags.FIF_DEBUGINFO,
                };
                ad7FramesInfo[lastIndex] = _ad7FrameInfoCreator.Create(null, info, _program);
            }

            return ad7FramesInfo;
        }
    }
}
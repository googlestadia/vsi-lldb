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

﻿using System.Collections.Generic;
using LldbApi;
using YetiCommon;
using DebuggerGrpcServer.RemoteInterfaces;
using DebuggerCommonApi;

namespace DebuggerGrpcServer
{
    public class RemoteThreadImpl : RemoteThread
    {
        public class Factory
        {
            private readonly RemoteFrameImpl.Factory remoteFrameFactory;

            public Factory(RemoteFrameImpl.Factory remoteFrameFactory)
            {
                this.remoteFrameFactory = remoteFrameFactory;
            }

            public RemoteThread Create(SbThread sbThread) =>
                sbThread != null ? new RemoteThreadImpl(sbThread, remoteFrameFactory) : null;
        }

        private readonly SbThread sbThread;
        private readonly RemoteFrameImpl.Factory remoteFrameFactory;

        private RemoteThreadImpl(SbThread sbThread, RemoteFrameImpl.Factory remoteFrameFactory)
        {
            this.sbThread = sbThread;
            this.remoteFrameFactory = remoteFrameFactory;
        }

        public SbProcess GetProcess() => sbThread.GetProcess();

        public string GetName() => sbThread.GetName();

        public ulong GetThreadId() => sbThread.GetThreadId();

        public string GetStatus() => sbThread.GetStatus();

        public void StepInto() => sbThread.StepInto();

        public void StepOver() => sbThread.StepOver();

        public void StepOut() => sbThread.StepOut();

        public void StepInstruction(bool stepOver) => sbThread.StepInstruction(stepOver);

        public uint GetNumFrames() => sbThread.GetNumFrames();

        public RemoteFrame GetFrameAtIndex(uint index) =>
            remoteFrameFactory.Create(sbThread.GetFrameAtIndex(index));

        public StopReason GetStopReason() => sbThread.GetStopReason().ConvertTo<StopReason>();

        public ulong GetStopReasonDataAtIndex(uint index) =>
            sbThread.GetStopReasonDataAtIndex(index);

        public uint GetStopReasonDataCount() => sbThread.GetStopReasonDataCount();

        public SbThread GetSbThread() => sbThread;

        public List<FrameInfoPair> GetFramesWithInfo(
            FrameInfoFlags fields, uint startIndex, uint maxCount)
        {
            var framesWithInfo = new List<FrameInfoPair>();

            /// This is a workaround to avoid calling <see cref="sbThread.GetNumFrames"/>.
            /// It is a very expensive call for cases when stack is significantly larger
            /// than <see cref="startIndex+maxCount"/>, because it traverses the whole stack.
            for (uint i = startIndex; i < startIndex + maxCount; ++i)
            {
                SbFrame ithFrame = sbThread.GetFrameAtIndex(i);

                if (ithFrame == null)
                {
                    // We have reached the end of stack, no need to load next frames.
                    break;
                }

                var frame = remoteFrameFactory.Create(ithFrame);
                framesWithInfo.Add(
                    new FrameInfoPair { Frame = frame, Info = frame.GetInfo(fields) });
            }

            return framesWithInfo;
        }
    }
}

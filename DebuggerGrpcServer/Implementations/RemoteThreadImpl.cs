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

using System.Collections.Generic;
using DebuggerCommonApi;
using DebuggerGrpcServer.RemoteInterfaces;
using LldbApi;
using YetiCommon;

namespace DebuggerGrpcServer
{
    public class RemoteThreadImpl : RemoteThread
    {
        public class Factory
        {
            readonly RemoteFrameImpl.Factory _remoteFrameFactory;

            public Factory(RemoteFrameImpl.Factory remoteFrameFactory)
            {
                _remoteFrameFactory = remoteFrameFactory;
            }

            public RemoteThread Create(SbThread sbThread) =>
                sbThread != null ? new RemoteThreadImpl(sbThread, _remoteFrameFactory) : null;
        }

        readonly SbThread _sbThread;
        readonly RemoteFrameImpl.Factory _remoteFrameFactory;

        RemoteThreadImpl(SbThread sbThread, RemoteFrameImpl.Factory remoteFrameFactory)
        {
            _sbThread = sbThread;
            _remoteFrameFactory = remoteFrameFactory;
        }

        public SbProcess GetProcess() => _sbThread.GetProcess();

        public string GetName() => _sbThread.GetName();

        public ulong GetThreadId() => _sbThread.GetThreadId();

        public string GetStatus() => _sbThread.GetStatus();

        public void StepInto() => _sbThread.StepInto();

        public void StepOver() => _sbThread.StepOver();

        public void StepOut() => _sbThread.StepOut();

        public void StepInstruction(bool stepOver) => _sbThread.StepInstruction(stepOver);

        public uint GetNumFrames() => _sbThread.GetNumFrames();

        public RemoteFrame GetFrameAtIndex(uint index) =>
            _remoteFrameFactory.Create(_sbThread.GetFrameAtIndex(index));

        public StopReason GetStopReason() => _sbThread.GetStopReason().ConvertTo<StopReason>();

        public ulong GetStopReasonDataAtIndex(uint index) =>
            _sbThread.GetStopReasonDataAtIndex(index);

        public uint GetStopReasonDataCount() => _sbThread.GetStopReasonDataCount();

        public SbThread GetSbThread() => _sbThread;

        public List<FrameInfoPair> GetFramesWithInfo(
            FrameInfoFlags fields, uint startIndex, uint maxCount)
        {
            var framesWithInfo = new List<FrameInfoPair>();

            /// This is a workaround to avoid calling <see cref="sbThread.GetNumFrames"/>.
            /// It is a very expensive call for cases when stack is significantly larger
            /// than <see cref="startIndex+maxCount"/>, because it traverses the whole stack.
            for (uint i = startIndex; i < startIndex + maxCount; ++i)
            {
                SbFrame ithFrame = _sbThread.GetFrameAtIndex(i);

                if (ithFrame == null)
                {
                    // We have reached the end of stack, no need to load next frames.
                    break;
                }

                var frame = _remoteFrameFactory.Create(ithFrame);
                framesWithInfo.Add(
                    new FrameInfoPair { Frame = frame, Info = frame.GetInfo(fields) });
            }

            return framesWithInfo;
        }
    }
}

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
using DebuggerGrpcClient;
using DebuggerGrpcClient.Interfaces;
using System;
using System.Collections.Generic;
using TestsCommon.TestSupport;

namespace YetiVSI.Test.TestSupport.Lldb
{
    public class RemoteTargetStub : RemoteTarget
    {
        public string Filename { get; }

        string _targetAttachErrorString;

        public RemoteTargetStub(string fileName, string targetAttachErrorString = null)
        {
            Filename = Filename;
            _targetAttachErrorString = targetAttachErrorString;
        }

        public SbProcess AttachToProcessWithID(SbListener listener, ulong pid, out SbError error)
        {
            var process = new SbProcessStub(this, listener, pid);
            error = _targetAttachErrorString == null
                        ? new SbErrorStub(true)
                        : new SbErrorStub(false, _targetAttachErrorString);
            return process;
        }

        public SbModule GetModuleAtIndex(int index)
        {
            throw new IndexOutOfRangeException();
        }

        public int GetNumModules() => 0;

        public SbProcess LoadCore(string coreFile)
        {
            return new SbProcessStub(this, coreFile);
        }

        #region Not Implemented
        public SbModule AddModule(string path, string triple, string uuid)
        {
            throw new NotImplementedTestDoubleException();
        }

        public RemoteBreakpoint BreakpointCreateByLocation(string file, uint line)
        {
            throw new NotImplementedTestDoubleException();
        }

        public RemoteBreakpoint BreakpointCreateByName(string symbolName)
        {
            throw new NotImplementedTestDoubleException();
        }

        public RemoteBreakpoint BreakpointCreateByAddress(ulong address)
        {
            throw new NotImplementedTestDoubleException();
        }

        public BreakpointErrorPair CreateFunctionOffsetBreakpoint(string symbolName, uint offset)
        {
            throw new NotImplementedTestDoubleException();
        }

        public bool BreakpointDelete(int breakpointId)
        {
            throw new NotImplementedTestDoubleException();
        }

        public bool DeleteWatchpoint(int watchId)
        {
            throw new NotImplementedTestDoubleException();
        }

        public RemoteBreakpoint FindBreakpointById(int id)
        {
            throw new NotImplementedTestDoubleException();
        }

        public long GetId()
        {
            throw new NotImplementedTestDoubleException();
        }

        public bool RemoveModule(SbModule module)
        {
            throw new NotImplementedTestDoubleException();
        }

        public SbAddress ResolveLoadAddress(ulong address)
        {
            throw new NotImplementedTestDoubleException();
        }

        public SbError SetModuleLoadAddress(SbModule module, long sectionsOffset)
        {
            throw new NotImplementedTestDoubleException();
        }

        public SbWatchpoint WatchAddress(long address, ulong size, bool read, bool write,
            out SbError error)
        {
            throw new NotImplementedTestDoubleException();
        }

        public List<InstructionInfo> ReadInstructionInfos(SbAddress address,
            uint count, string flavor)
        {
            throw new NotImplementedTestDoubleException();
        }
        #endregion
    }
}

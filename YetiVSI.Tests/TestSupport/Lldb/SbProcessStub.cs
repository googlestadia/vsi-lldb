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
using System;
using TestsCommon.TestSupport;

namespace YetiVSI.Test.TestSupport.Lldb
{
    public class SbProcessStub : SbProcess
    {
        readonly RemoteTarget target;
        readonly string coreFileName;
        readonly SbListener listener;
        readonly ulong pid;
        SbUnixSignalsStub signalStub;

        public SbProcessStub(RemoteTarget target, SbUnixSignalsStub signalStub)
        {
            this.target = target;
            if (signalStub == null)
            {
                signalStub = new SbUnixSignalsStub();
            }
            else
            {
                this.signalStub = signalStub;
            }
        }

        public SbProcessStub(RemoteTarget target, string coreFileName)
        {
            this.target = target;
            this.coreFileName = coreFileName;
            signalStub = new SbUnixSignalsStub();
        }

        public SbProcessStub(RemoteTarget target, SbListener listener, ulong pid)
        {
            this.target = target;
            this.listener = listener;
            this.pid = pid;
            signalStub = new SbUnixSignalsStub();
        }

        public string CoreFileName => coreFileName;

        public SbListener Listener => listener;

        public ulong Pid => pid;

        public RemoteTarget GetTarget() => target;

        public SbUnixSignals GetUnixSignals()
        {
            return signalStub;
        }

        #region Not Implemented

        public bool Continue()
        {
            throw new NotImplementedTestDoubleException();
        }

        public bool Detach(bool keepStopped)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int GetNumThreads()
        {
            throw new NotImplementedTestDoubleException();
        }

        public RemoteThread GetSelectedThread()
        {
            throw new NotImplementedTestDoubleException();
        }

        public RemoteThread GetThreadAtIndex(int index)
        {
            throw new NotImplementedTestDoubleException();
        }

        public RemoteThread GetThreadById(ulong id)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int GetUniqueId()
        {
            throw new NotImplementedTestDoubleException();
        }

        public bool Kill()
        {
            throw new NotImplementedTestDoubleException();
        }

        public ulong ReadMemory(ulong address, byte[] buffer, ulong size, out SbError error)
        {
            throw new NotImplementedTestDoubleException();
        }

        public bool SetSelectedThreadById(ulong threadId)
        {
            throw new NotImplementedTestDoubleException();
        }

        public bool Stop()
        {
            throw new NotImplementedTestDoubleException();
        }

        public ulong WriteMemory(ulong address, byte[] buffer, ulong size, out SbError error)
        {
            throw new NotImplementedTestDoubleException();
        }

        public void SaveCore(string dumpPath, out SbError error)
        {
            throw new NotImplementedTestDoubleException();
        }
        #endregion
    }
}

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
using DebuggerGrpcClient;
using TestsCommon.TestSupport;

namespace YetiVSI.Test.TestSupport.Lldb
{
    public class GrpcListenerFactoryFake : GrpcListenerFactory
    {
        public override SbListener Create(GrpcConnection connection, string name)
        {
            return new SbListenerStub(name);
        }
    }

    public class SbListenerStub : SbListener
    {
        public SbListenerStub(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public long GetId()
        {
            throw new NotImplementedTestDoubleException();
        }

        public bool WaitForEvent(uint numSeconds, out SbEvent evnt)
        {
            throw new NotImplementedTestDoubleException();
        }
    }
}

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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TestsCommon.TestSupport;

namespace YetiVSI.Test.TestSupport.Lldb
{
    class CompileExpressionEntry
    {
        public Tuple<SbType, SbError> Result { get; }
        public Dictionary<string, SbType> ContextArgs { get; }
        public CompileExpressionEntry(Tuple<SbType, SbError> result,
                                      Dictionary<string, SbType> contextArgs)
        {
            Result = result;
            ContextArgs = contextArgs;
        }
    };

    public class RemoteTargetStub : RemoteTarget
    {
        public string Filename { get; }

        string _targetAttachErrorString;

        IDictionary<string, Queue<CompileExpressionEntry>> _nextExpressions;

        public RemoteTargetStub(string fileName, string targetAttachErrorString = null)
        {
            Filename = fileName;
            _targetAttachErrorString = targetAttachErrorString;
            _nextExpressions = new Dictionary<string, Queue<CompileExpressionEntry>>();
        }

        public SbProcess AttachToProcessWithID(SbListener listener, ulong pid, out SbError error)
        {
            var process = new SbProcessStub(this, listener, pid);
            error = _targetAttachErrorString == null
                        ? new SbErrorStub(true)
                        : new SbErrorStub(false, _targetAttachErrorString);
            return process;
        }

        public SbModule GetModuleAtIndex(int index) => throw new IndexOutOfRangeException();

        public int GetNumModules() => 0;

        public SbProcess LoadCore(string coreFile) => new SbProcessStub(this, coreFile);

        public EventType AddListener(SbListener listener,
                                     TargetEventType eventMask) => (EventType)eventMask;

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

        public Task<Tuple<SbType, SbError>> CompileExpressionAsync(
            SbType scope, string expression, IDictionary<string, SbType> contextArgs)
        {
            if (!_nextExpressions.TryGetValue(expression, out var queue) || queue.Count == 0)
            {
                var error = new SbErrorStub(false, $"(Test) Unexpected expression {expression}");
                return Task.FromResult(Tuple.Create<SbType, SbError>(null, error));
            }

            CompileExpressionEntry entry = queue.Dequeue();
            if (!CompareContextArguments(contextArgs, entry.ContextArgs))
            {
                var error = new SbErrorStub(
                    false, $"(Test) Unexpected context arguments for expression {expression}");
                return Task.FromResult(Tuple.Create<SbType, SbError>(null, error));
            }

            return Task.FromResult(entry.Result);
        }

        public void AddTypeFromExpression(SbType resultType, string expression,
                                          IDictionary<string, SbType> contextArgs = null)
        {
            var result = Tuple.Create<SbType, SbError>(resultType, null);
            AddNextExpressionEntry(result, expression, contextArgs);
        }

        public void AddErrorFromExpression(SbErrorStub error, string expression,
                                           IDictionary<string, SbType> contextArgs = null)
        {
            var result = Tuple.Create<SbType, SbError>(null, error);
            AddNextExpressionEntry(result, expression, contextArgs);
        }

        public bool CheckNoPendingExpressions()
        {
            return _nextExpressions.Values.All(q => q.Count == 0);
        }

        void AddNextExpressionEntry(Tuple<SbType, SbError> result, string expression,
                                    IDictionary<string, SbType> contextArgs)
        {

            Queue<CompileExpressionEntry> queue;
            if (!_nextExpressions.TryGetValue(expression, out queue))
            {
                queue = new Queue<CompileExpressionEntry>();
                _nextExpressions[expression] = queue;
            }
            // Clone context args, as the object could be modified as tests are running.
            var contextArgsCopy = contextArgs != null ? new Dictionary<string, SbType>(contextArgs)
                                                      : new Dictionary<string, SbType>();
            queue.Enqueue(new CompileExpressionEntry(result, contextArgsCopy));
        }

        bool CompareContextArguments(IDictionary<string, SbType> lhs,
                                     IDictionary<string, SbType> rhs)
        {
            if (lhs.Count != rhs.Count)
            {
                return false;
            }

            foreach (var key in lhs.Keys)
            {
                if (!rhs.TryGetValue(key, out var rhsValue))
                {
                    return false;
                }
                var lhsValue = lhs[key];
                // Comparing type names.
                if (lhsValue.GetName() != rhsValue.GetName())
                {
                    return false;
                }
            }

            return true;
        }

#endregion
    }
}

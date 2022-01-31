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

using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio;

namespace YetiVSI.PortSupplier
{
    class ProcessesEnum : DebugEnum<IDebugProcess2, IEnumDebugProcesses2>, IEnumDebugProcesses2
    {
        public ProcessesEnum(IDebugProcess2[] data) : base(data)
        {
        }
    }

    class PortsEnum : DebugEnum<IDebugPort2, IEnumDebugPorts2>, IEnumDebugPorts2
    {
        public PortsEnum(IDebugPort2[] data) : base(data)
        {
        }
    }

    class ProgramsEnum : DebugEnum<IDebugProgram2, IEnumDebugPrograms2>, IEnumDebugPrograms2
    {
        public ProgramsEnum(IDebugProgram2[] data) : base(data)
        {
        }
    }

    internal class DebugEnum<T, I> where I : class
    {
        private readonly T[] data;
        private uint position;

        public DebugEnum(T[] data)
        {
            this.data = data;
            this.position = 0;
        }

        public int Clone(out I instance)
        {
            instance = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetCount(out uint count)
        {
            count = (uint)data.Length;
            return VSConstants.S_OK;
        }

        public int Next(uint num, T[] results, ref uint numFetched)
        {
            return Move(num, results, ref numFetched);
        }

        public int Reset()
        {
            lock (this)
            {
                position = 0;
                return VSConstants.S_OK;
            }
        }

        public int Skip(uint num)
        {
            uint numFetched = 0;
            return Move(num, null, ref numFetched);
        }

        private int Move(uint num, T[] results, ref uint numFetched)
        {
            lock (this)
            {
                int result = VSConstants.S_OK;
                numFetched = (uint)data.Length - position;

                if (num > numFetched)
                {
                    result = VSConstants.S_FALSE;
                }
                else if (num < numFetched)
                {
                    numFetched = num;
                }

                if (results != null)
                {
                    for (int c = 0; c < numFetched; c++)
                    {
                        results[c] = data[position + c];
                    }
                }

                position += numFetched;
                return result;
            }
        }
    }
}

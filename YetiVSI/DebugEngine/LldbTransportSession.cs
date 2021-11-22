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

using System;
using System.IO.MemoryMappedFiles;
using YetiCommon;
using YetiVSI.DebugEngine.Interfaces;

namespace YetiVSI.DebugEngine
{
    // Used to coordinate state between multiple LLDB transport instances.
    public class LldbTransportSession : ITransportSession
    {
        public const int INVALID_SESSION_ID = -1;
        const int MAX_SESSIONS = 10;
        const string FILE_PREFIX = "YetiTransportSession";

        // The sessionId is used as the index into an array of ports.
        readonly int sessionId;
        MemoryMappedFile memoryMappedFile;

        bool disposed = false;

        public LldbTransportSession()
        {
            sessionId = FindAvailableSessionId();
        }

        #region ITransportSession

        public int GetSessionId()
        {
            ThrowIfDisposed();
            return sessionId;
        }

        public int GetLocalDebuggerPort()
        {
            ThrowIfDisposed();
            return WorkstationPorts.LLDB_SERVERS[sessionId];
        }

        public int GetRemoteDebuggerPort()
        {
            ThrowIfDisposed();
            return WorkstationPorts.REMOTE_LLDB_SERVER;
        }

        public int GetReservedLocalAndRemotePort()
        {
            ThrowIfDisposed();
            return WorkstationPorts.REMOTE_DEPLOY_AND_LLDB_GDB_SERVERS[sessionId];
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                memoryMappedFile.Dispose();
                memoryMappedFile = null;
            }
            disposed = true;
        }

        #endregion

        public bool IsValid()
        {
            ThrowIfDisposed();
            return sessionId != INVALID_SESSION_ID && memoryMappedFile != null;
        }

        // Throws a System.ObjectDisposedException if the object has been disposed.
        void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(GetType().ToString());
            }
        }

        int FindAvailableSessionId()
        {
            for (int i = 0; i < MAX_SESSIONS; i++)
            {
                try
                {
                    // Create a memory mapped file in system memory, which will exist as long as
                    // the debug engine is running.  They will either be cleaned up when we call
                    // dispose on this object, or when the process stops.  These files let transport
                    // sessions 'reserve' a system unique session ID.  If the file exists it will
                    // throw a System.IO.IOException, and we can try the next one.
                    memoryMappedFile = MemoryMappedFile.CreateNew(FILE_PREFIX + i, sizeof(byte));
                    return i;
                }
                catch (System.IO.IOException)
                {
                }
            }
            return INVALID_SESSION_ID;
        }
    }
}

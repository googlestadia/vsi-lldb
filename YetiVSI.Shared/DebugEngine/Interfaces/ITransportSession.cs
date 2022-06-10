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

namespace YetiVSI.DebugEngine.Interfaces
{
    /// <summary>
    /// A transport session coordinates system-wide port allocations for debugger
    /// sessions. Each debug session has one locally reserved port (GetDebuggerPort)
    /// and one port that is reserved both locally and remotely. The transport session
    /// ensures that the ports used by debugging sessions from one workstation do not
    /// clash.
    ///
    /// The sessions are used for two purposes:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// LLDB debugger session uses the local port as an ssh tunntl entry point for
    /// the lldb-server session, and the local and remote port as the gdb-server
    /// port (because of the limitations of the LLDB protocol, the port number must
    /// be the same locally and remotely).
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Compressed binary deployment uses the local and remote port for transferring
    /// binaries. Note that this use will not limit the number of available sessions
    /// because we never debug and deploy in the same session concurrently.
    /// </description>
    /// </item>
    /// </list>
    /// </summary>
    public interface ITransportSession : IDisposable
    {
        /// <summary>
        /// The ID of the session.
        /// </summary>
        int GetSessionId();

        /// <summary>
        /// The local port number for the LLDB debugger.
        /// </summary>
        int GetLocalDebuggerPort();

        /// <summary>
        /// The remote port number of the LLDB debugger.
        /// </summary>
        int GetRemoteDebuggerPort();

        /// <summary>
        /// Gets the reserved local and remote reserved port for the session.
        /// </summary>
        int GetReservedLocalAndRemotePort();
    }
}

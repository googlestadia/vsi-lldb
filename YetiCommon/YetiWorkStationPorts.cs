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
using System.Collections.Generic;
using System.Linq;

namespace YetiCommon
{
    // Tracks local port numbers used by developer tools.
    public class WorkstationPorts
    {
        // ------------------ WORKSTATION --------------------------------------------------------
        // These are local port only used on the workstation.


        public static readonly List<int> LLDB_SERVERS =
            Enumerable.Range(LLDB_SERVER_FIRST, LLDB_SERVER_LAST - LLDB_SERVER_FIRST).ToList();

        // Reserve ports 44420 - 44429 for use by LLDB servers.
        public const int LLDB_SERVER_FIRST = 44420;
        public const int LLDB_SERVER_LAST = 44429;

        // Reserve port 44430 for use by MountRemote.
        public const int MOUNT_REMOTE = 44430;

        // Reserve port 44431 for use by the ggp CLI for the webserver used during login.
        public const int GGP_CLI = 44431;

        // Reserve ports 44432-44442 for use by the local assets stream manager.
        public const int LOCAL_ASSETS_STREAM_MANAGER_FIRST = 44432;
        public const int LOCAL_ASSETS_STREAM_MANAGER_LAST = 44442;

        // Reserve ports 44443 and 44446 for use by the launch controller.
        public const int LAUNCH_CONTROLLER_WEB = 44443;
        public const int LAUNCH_CONTROLLER = 44446;

        // Reserve port 44444 for use by the Process Manager local gRPC server.
        public const int PROCESS_MANAGER = 44444;

        // Reserve port 44445 for use by the System Tray App local gRPC server.
        public const int SYSTEM_TRAY_APP = 44445;

        // Reserve ports 44448 and 44449 for use by the SDK Proxy.
        public const int SDK_PROXY = 44448;
        public const int SDK_PROXY_WEB = 44449;

        // Reserve ports 44450-44459 for ggp_sync.
        public const int GGP_RSYNC_FIRST = 44450;
        public const int GGP_RSYNC_LAST = 44459;

        // Reserve ports 27300, 38920 for rgp and renderdoc.
        public const int RGP_LOCAL = 27300;
        public const int RENDERDOC_LOCAL = 38920;

        // ------------------ GAMELET LOCAL -------------------------------------------------------
        // These are ports used locally on the gamelet.


        // Ports for the debugger and for compressed remote deploy.
        public static readonly List<int> REMOTE_DEPLOY_AND_LLDB_GDB_SERVERS =
            Enumerable.Range(REMOTE_DEPLOY_AND_LLDB_GDB_SERVER_FIRST,
                REMOTE_DEPLOY_AND_LLDB_GDB_SERVER_LAST -
                     REMOTE_DEPLOY_AND_LLDB_GDB_SERVER_FIRST).ToList();
        public const int REMOTE_LLDB_SERVER = 44500;
        public const int REMOTE_DEPLOY_AND_LLDB_GDB_SERVER_FIRST = 44510;
        public const int REMOTE_DEPLOY_AND_LLDB_GDB_SERVER_LAST = 44519;

        // ------------------ GAMELET EXTERNAL ----------------------------------------------------
        // These are ports used on the gamelet and are not blocked by the firewall.


        // Reserve ports 27300, 38920 for rgp and renderdoc.
        public const int RGP_REMOTE = 27300;
        public const int RENDERDOC_REMOTE = 38920;

        // TODO: Move this port to the workstation range
        // Reserve port 44730 for use by the Metrics Uploader service.
        public const int METRICS_UPLOADER = 44730;
    }
}

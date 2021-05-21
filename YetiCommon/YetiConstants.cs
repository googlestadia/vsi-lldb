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
using System.IO;

namespace YetiCommon
{
    public class YetiConstants
    {
        public static readonly Guid CommandSetGuid =
            new Guid("56bb93d0-9d14-42a1-afab-ee7ed1d3ca1a");

        // This must match the Guid for the Port Supplier in the pkgdef.
        public static readonly Guid PortSupplierGuid =
            new Guid("{ed7758db-7076-4b0d-b004-81e6fc6a6af9}");

        // This must match the Guid for the Debug Engine in the pkgdef.
        public static readonly Guid DebugEngineGuid =
            new Guid("{6e5c4e9a-119b-4cbf-8c39-24f304d34655}");

        public static readonly Guid ExceptionEventGuid =
            new Guid("51A94113-8788-4A54-AE15-08B74FF922D0");

        public static readonly string Command = "cmd.exe";

        public static readonly string DebuggerGrpcServerExecutable = "DebuggerGrpcServer.exe";

        public static readonly string SshKeygenWinExecutable = "ssh-keygen.exe";
        public static readonly string SshWinExecutable = "ssh.exe";
        public static readonly string ScpWinExecutable = "scp.exe";
        public static readonly string ObjDumpWinExecutable = "llvm-objdump.exe";
        public static readonly string ObjDumpLinuxExecutable = "objdump";
        public static readonly string LldbServerLinuxExecutable = "lldb-server";

        public static readonly string YetiTitle = "Stadia";

        public static readonly string VsixManifest = "extension.vsixmanifest";

        public static readonly string[] SymbolServerExcludeList = { "msdl.microsoft.com" };

        /// <summary>
        /// Directory where the current code is executed from.
        /// In production, this is the GGP extension directory.
        /// For tests, it is the directory that contains the test DLL.
        /// </summary>
        public static readonly string RootDir =
            Path.GetDirectoryName(typeof(YetiConstants).Assembly.Location);

        public static readonly string RemoteToolsBinDir = "/opt/developer/tools/bin/";
        public static readonly string RemoteDeployPath = "/mnt/developer/";

        public static readonly string DeveloperMountingPoint = "/mnt/developer";
        public static readonly string PackageMountingPoint = "/mnt/package";
        public static readonly string GameAssetsMountingPoint = "/srv/game/assets";
        public static readonly string LldbDir = Path.Combine(RootDir, "LLDB");

#if INTERNAL_BUILD
        public static readonly string LldbServerLinuxPath = RemoteToolsBinDir;
#else
        public static readonly string LldbServerLinuxPath = "/usr/local/cloudcast/bin/";
#endif
    }
}

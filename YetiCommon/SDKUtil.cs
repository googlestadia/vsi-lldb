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
using System.IO;

namespace YetiCommon
{
    public class SDKUtil
    {
        // Returns a path to the global user configuration directory of the Yeti SDK.
        public static string GetUserConfigPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GGP");
        }

        // Returns a path to the SSH configuration directory of the Yeti SDK.
        public static string GetSshConfigPath()
        {
            return Path.Combine(GetUserConfigPath(), "ssh");
        }

        // Returns a path to the dev tools ssh key file.
        public static string GetSshKeyFilePath()
        {
            return Path.Combine(GetSshConfigPath(), "id_rsa");
        }

        // Returns a path to the dev tools ssh known_hosts file.
        public static string GetSshKnownHostsFilePath()
        {
            return Path.Combine(GetSshConfigPath(), "known_hosts");
        }

        // Returns a path to the user's credentials folder.
        public static string GetCredentialsPath()
        {
            return Path.Combine(GetUserConfigPath(), "credentials");
        }

        public static string GetLoggingPath()
        {
            return Path.Combine(GetUserConfigPath(), "logs");
        }

        public static string GetSystem32Path()
        {
            string winPath = Environment.ExpandEnvironmentVariables("%SystemRoot%");
            return Path.Combine(winPath, "system32");
        }

        // Returns a path to the installation dir of the Yeti SDK.
        public static string GetSDKPath()
        {
            var path = Environment.GetEnvironmentVariable("GGP_SDK_PATH");
            if (!string.IsNullOrEmpty(path))
            {
                return FileUtil.RemoveTrailingSeparator(path);
            }
            var programFiles = Environment.ExpandEnvironmentVariables("%ProgramW6432%");
            return Path.Combine(programFiles, "GGP SDK");
        }

        // Returns a path to the tools dir of the Yeti SDK.
        public static string GetSDKToolsPath()
        {
            var sdkPath = GetSDKPath();
            return Path.Combine(sdkPath, "dev", "bin");
        }

        public static string GetToolchainBinariesPath()
        {
            var sdkPath = GetSDKPath();
            return Path.Combine(sdkPath, "BaseSDK", "LLVM", "9.0.1", "bin");
        }

        // Returns a path to the SSH dir packaged with the Yeti SDK.
        public static string GetSshPath()
        {
            var sdkPath = GetSDKPath();
            return Path.Combine(sdkPath, "tools", "OpenSSH-Win64");
        }

        // Returns a list of all paths within the Yeti SDK that contain target libraries.
        public static List<string> GetLibraryPaths()
        {
            string[] paths =
            {
                "\\sysroot\\lib\\x86_64-linux-gnu",
                "\\sysroot\\usr\\lib\\x86_64-linux-gnu",
                "\\sysroot\\usr\\lib",
                "\\sysroot\\lib",
                "\\BaseSDK\\ggp\\lib",
            };

            var sdkPath = GetSDKPath();
            var result = new List<string>();
            foreach (var path in paths)
            {
                result.Add(sdkPath + path);
            }
            return result;
        }

        // Returns a path to the local application data directory of the Yeti SDK.
        public static string GetLocalAppDataPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GGP");
        }

        // Returns the default symbol cache directory
        public static string GetDefaultSymbolCachePath()
        {
            return Path.Combine(GetLocalAppDataPath(), "SymbolCache");
        }

        // Returns the default symbol store directory
        public static string GetDefaultSymbolStorePath()
        {
            return Path.Combine(GetLocalAppDataPath(), "SymbolStore");
        }
    }
}

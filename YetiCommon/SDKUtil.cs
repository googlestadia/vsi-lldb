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
        /// <summary>
        /// Returns a path to the global user configuration directory of the Yeti SDK. 
        /// </summary>
        public static string GetUserConfigPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GGP");
        }

        /// <summary>
        /// Returns a path to the SSH configuration directory of the Yeti SDK. 
        /// </summary>
        public static string GetSshConfigPath()
        {
            return Path.Combine(GetUserConfigPath(), "ssh");
        }

        /// <summary>
        /// Returns a path to the dev tools ssh key file.
        /// </summary>
        public static string GetSshKeyFilePath()
        {
            return Path.Combine(GetSshConfigPath(), "id_rsa");
        }

        /// <summary>
        /// Returns a path to the dev tools ssh known_hosts file.
        /// </summary>
        public static string GetSshKnownHostsFilePath()
        {
            return Path.Combine(GetSshConfigPath(), "known_hosts");
        }

        /// <summary>
        /// Returns a path to the user's credentials folder.
        /// </summary>
        public static string GetCredentialsPath()
        {
            return Path.Combine(GetUserConfigPath(), "credentials");
        }

        /// <summary>
        /// Returns a path to the folder containing SDK tool logs.
        /// </summary>
        public static string GetLoggingPath()
        {
            return Path.Combine(GetUserConfigPath(), "logs");
        }

        /// <summary>
        /// Returns a path to the installation dir of the Yeti SDK.
        /// </summary>
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

        /// <summary>
        /// Returns a path to the tools dir of the Yeti SDK.
        /// </summary>
        public static string GetSDKToolsPath()
        {
            var sdkPath = GetSDKPath();
            return Path.Combine(sdkPath, "dev", "bin");
        }

        /// <summary>
        /// Returns a path to the SSH dir packaged with the Yeti SDK.
        /// </summary>
        public static string GetSshPath()
        {
            var sdkPath = GetSDKPath();
            return Path.Combine(sdkPath, "tools", "OpenSSH-Win64");
        }

        /// <summary>
        /// Returns a path to the dir that contains Orbit.
        /// </summary>
        public static string GetOrbitPath()
        {
            var sdkPath = GetSDKPath();
            return Path.Combine(sdkPath, "tools", "Orbit");
        }

        /// <summary>
        /// Returns a path to the dir that contains Dive.
        /// </summary>
        public static string GetDivePath()
        {
            var sdkPath = GetSDKPath();
            return Path.Combine(sdkPath, "tools", "Dive");
        }

        /// <summary>
        /// Returns a list of all paths within the Yeti SDK that contain target libraries.
        /// </summary>
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

        /// <summary>
        /// Returns a path to the local application data directory of the Yeti SDK.
        /// </summary>
        public static string GetLocalAppDataPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GGP");
        }

        /// <summary>
        /// Returns the default symbol cache directory.
        /// </summary>
        public static string GetDefaultSymbolCachePath()
        {
            return Path.Combine(GetLocalAppDataPath(), "SymbolCache");
        }

        /// <summary>
        /// Returns the default symbol store directory.
        /// </summary>
        public static string GetDefaultSymbolStorePath()
        {
            return Path.Combine(GetLocalAppDataPath(), "SymbolStore");
        }

        /// <summary>
        /// Returns the path of the SDK services configuration, e.g.
        /// %APPDATA%\GGP\services.
        /// </summary>
        public static string GetServicesConfigPath()
        {
            return Path.Combine(GetUserConfigPath(), "services");
        }
    }
}
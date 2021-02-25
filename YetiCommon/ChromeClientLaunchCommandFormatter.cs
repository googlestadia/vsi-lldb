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
using System.Text;

namespace YetiCommon
{
    /// <summary>
    /// This class provides helper functions for creating and parsing chrome
    /// client launch commands.
    /// </summary>
    public class ChromeClientLaunchCommandFormatter
    {
        readonly ISerializer serializer;
        readonly string launcherPath;

        public ChromeClientLaunchCommandFormatter(ISerializer serializer)
            : this(serializer, YetiConstants.RootDir)
        {
        }

        public ChromeClientLaunchCommandFormatter(ISerializer serializer, string launcherDir)
        {
            if (launcherDir == null) { throw new ArgumentNullException(nameof(launcherDir)); }

            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            this.launcherPath = Path.Combine(launcherDir, "ChromeClientLauncher.exe");
        }

        /// <summary>
        /// Create a launch command that can execute using Cmd.exe
        /// </summary>
        public string CreateFromParams(ChromeLaunchParams launchParams)
            => $"/c \"\"{launcherPath}\" {EncodeLaunchParams(launchParams)}\"";

        /// <summary>
        /// Create a launch command that can execute using Cmd.exe
        /// </summary>
        public string CreateWithLaunchName(ChromeLaunchParams launchParams,
                                           string launchName) =>
            $"/c \"\"{launcherPath}\" {EncodeLaunchParams(launchParams)} " +
            $"{EncodeLaunchName(launchName)}\"";

        /// <summary>
        /// Parse a launch command and return the launch arguments.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if the command is malformed</exception>
        /// <exception cref="ArgumentNullException">Thrown if the command is null</exception>
        public void Parse(string command, out ChromeLaunchParams launchParams,
                          out string launchName)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            var ccCmd = $"/c \"\"{launcherPath}\" ";
            if (!command.StartsWith(ccCmd))
            {
                throw new ArgumentException($"launch command is malformed: {command}");
            }

            var args = command.Substring(ccCmd.Length);
            args = args.Substring(0, args.Length - 1);

            string launchParamsArg = args;
            string launchNameArg = null;
            if (args.Contains(" "))
            {
                launchParamsArg = args.Substring(0, args.IndexOf(" ", StringComparison.Ordinal));
                launchNameArg = args.Substring(args.IndexOf(" ", StringComparison.Ordinal));
            }

            launchParams = DecodeLaunchParams(launchParamsArg);
            launchName = launchNameArg == null
                ? null
                : Encoding.UTF8.GetString(Convert.FromBase64String(launchNameArg));
        }

        /// <summary>
        /// Decode a base64 encoded representation of launch parameters.
        /// </summary>
        public ChromeLaunchParams DecodeLaunchParams(string encodedParams) =>
            serializer.Deserialize<ChromeLaunchParams>(
                Encoding.UTF8.GetString(
                    Convert.FromBase64String(encodedParams)));

        /// <summary>
        /// base64 encode launch parameters
        /// </summary>
        public string EncodeLaunchParams(ChromeLaunchParams launchParams) =>
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes(
                    serializer.Serialize(launchParams)));

        /// <summary>
        /// base64 encode launch name
        /// </summary>
        public string EncodeLaunchName(string launchName) =>
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes(launchName));
    }
}

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
using System.Diagnostics;
using System.IO;
using System.Text;
using YetiCommon;
using YetiCommon.Cloud;

namespace ChromeClientLauncher
{
    class Program
    {
        static void Main(string[] args)
        {
            var consoleTraceListener = new ConsoleTraceListener
            {
                Name = "mainConsoleTracer"
            };
            Trace.Listeners.Add(consoleTraceListener);

            try
            {
                var jsonUtil = new JsonUtil();

                var gameLauncher = new ChromeTestClientLauncher.Factory(
                    new ChromeClientLaunchCommandFormatter(jsonUtil),
                    new SdkConfig.Factory(jsonUtil),
                    new ChromeLauncher(new BackgroundProcess.Factory())).Create(args[0]);

                // new launch api is enabled.
                if (args.Length == 2)
                {
                    string launchName = Encoding.UTF8.GetString(Convert.FromBase64String(args[1]));
                    string launchUrl = gameLauncher.BuildLaunchUrlWithLaunchName(launchName);
                    gameLauncher.LaunchGame(launchUrl, Directory.GetCurrentDirectory());
                }
                else
                {
                    ConfigStatus urlBuildStatus = gameLauncher.BuildLaunchUrl(out string launchUrl);
                    if (urlBuildStatus.IsWarningLevel)
                    {
                        Console.WriteLine($"Warning: {urlBuildStatus.WarningMessage}");
                    }
                    else if (!urlBuildStatus.IsOk)
                    {
                        throw new NotImplementedException();
                    }

                    gameLauncher.LaunchGame(launchUrl, Directory.GetCurrentDirectory());
                }
            }
            catch (Exception ex) when (LogException(ex))
            {
            }
        }

        static bool LogException(Exception ex)
        {
            Console.Error.WriteLine($"Unable to launch chrome client, reason: {ex.Message}");
            return false;
        }
    }
}
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

﻿using System;
using System.Diagnostics;
using System.IO;
using YetiCommon;

namespace ChromeClientLauncher
{
    class Program
    {
        static void Main(string[] args)
        {
            ConsoleTraceListener consoleTraceListener = new ConsoleTraceListener();
            consoleTraceListener.Name = "mainConsoleTracer";
            Trace.Listeners.Add(consoleTraceListener);

            try
            {
                var jsonUtil = new JsonUtil();
                var gameLauncher = new YetiCommon.ChromeClientLauncher.Factory(
                    new BackgroundProcess.Factory(),
                    new ChromeClientLaunchCommandFormatter(jsonUtil),
                    new SdkConfig.Factory(jsonUtil)).Create(args[0]);
                var urlBuildStatus = gameLauncher.BuildLaunchUrl(out string launchUrl);
                // TODO: Currently the status severity can not be higher than Warning.
                // Once the Game Launch Api is used, the Error severity
                // case should be implemented. http://(internal)
                if (urlBuildStatus.IsWarningLevel)
                {
                    Console.WriteLine($"Warning: {urlBuildStatus.WarningMessage}");
                }
                else if (!urlBuildStatus.IsOk)
                {
                    throw new NotImplementedException();
                }

                gameLauncher.StartChrome(launchUrl, Directory.GetCurrentDirectory());
            }
            catch (Exception ex) when (LogException(ex))
            {
            }
        }

        private static bool LogException(Exception ex)
        {
            Console.Error.WriteLine($"Unable to launch chrome client, reason: {ex.Message}");
            return false;
        }
    }
}

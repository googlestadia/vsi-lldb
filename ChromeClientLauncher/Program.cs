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
using System.Linq;
using System.Text;
using YetiCommon;
using YetiVSI.ProjectSystem.Abstractions;

namespace ChromeClientLauncher
{
    class Program
    {
        static void Main(string[] args)
        {
            var consoleTraceListener = new ConsoleTraceListener { Name = "mainConsoleTracer" };
            Trace.Listeners.Add(consoleTraceListener);

            try
            {
                var gameLauncher =
                    new ChromeClientsLauncher
                        .Factory(new ChromeClientLaunchCommandFormatter(),
                                 new SdkConfig.Factory(new JsonUtil()),
                                 new ChromeLauncher(new BackgroundProcess.Factory()))
                        .Create(args[0]);

                if (gameLauncher.LaunchParams.Endpoint == StadiaEndpoint.AnyEndpoint)
                {
                    return;
                }

                string launchName = Encoding.UTF8.GetString(Convert.FromBase64String(args[1]));
                string launchUrl;

                switch (gameLauncher.LaunchParams.Endpoint)
                {
                    case StadiaEndpoint.TestClient:
                        {
                            launchUrl = gameLauncher.MakeTestClientUrl(launchName);
                            break;
                        }
                    case StadiaEndpoint.PlayerEndpoint:
                        {
                            string launchId = launchName.Split('/').Last();
                            launchUrl = gameLauncher.MakePlayerClientUrl(launchId);
                            break;
                        }
                    default:
                        throw new ArgumentOutOfRangeException(
                            $"Endpoint is not supported: {gameLauncher.LaunchParams.Endpoint}");
                }

                gameLauncher.LaunchGame(launchUrl, Directory.GetCurrentDirectory());
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Unable to launch chrome client, reason: {ex.Message}");
            }
        }
    }
}
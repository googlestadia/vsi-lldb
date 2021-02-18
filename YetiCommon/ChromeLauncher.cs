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

namespace YetiCommon
{
    public interface IChromeLauncher
    {
        void StartChrome(string url, string workingDirectory, string profileDirectory);
    }

    public class ChromeLauncher : IChromeLauncher
    {
        readonly BackgroundProcess.Factory _backgroundProcessFactory;

        public ChromeLauncher(BackgroundProcess.Factory backgroundProcessFactory)
        {
            _backgroundProcessFactory = backgroundProcessFactory;
        }

        public void StartChrome(string url, string workingDirectory, string profileDirectory)
        {
            profileDirectory = string.IsNullOrEmpty(profileDirectory)
                ? "Default"
                : profileDirectory;

            StartProcess(workingDirectory, $"start chrome \"{url}\"", "--new-window",
                         $"--profile-directory=\"{profileDirectory}\"");
        }

        void StartProcess(string workingDirectory, string command, params string[] args) =>
            _backgroundProcessFactory.Create(YetiConstants.Command,
                                             $"/c \"{command} {string.Join(" ", args)}\"",
                                             workingDirectory).Start();
    }
}
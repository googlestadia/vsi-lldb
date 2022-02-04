// Copyright 2022 Google LLC
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

using System.IO;
using System.IO.Abstractions;
using YetiCommon;

namespace YetiVSI.Orbit
{
    public interface IOrbitLauncher
    {
        /// <summary>
        /// Returns the full path of the Orbit binary.
        /// </summary>
        string OrbitBinaryPath { get; }

        /// <summary>
        /// Launches Orbit and instructs it to attach to the specified binary.
        /// </summary>
        /// <param name="gameletExecutablePath">Full path to the game binary</param>
        /// <param name="gameletId">ID of the gamelet that runs the binary</param>
        /// <exception cref="ProcessException">If Orbit failed to launch</exception>
        void Launch(string gameletExecutablePath, string gameletId);

        /// <summary>
        /// Returns true if the Orbit component was installed during SDK installation.
        /// </summary>
        bool IsOrbitInstalled();
    }

    public class OrbitLauncher : IOrbitLauncher
    {
        public string OrbitBinaryPath => Path.Combine(SDKUtil.GetOrbitPath(), "Orbit.exe");

        readonly IFileSystem _fileSystem;
        readonly BackgroundProcess.Factory _backgroundProcessFactory;

        public OrbitLauncher(BackgroundProcess.Factory backgroundProcessFactory,
                             IFileSystem fileSystem)
        {
            _backgroundProcessFactory = backgroundProcessFactory;
            _fileSystem = fileSystem;
        }

        public void Launch(string gameletExecutablePath, string gameletId)
        {
            string args = $"--connection_target={gameletExecutablePath}@{gameletId}";
            IBackgroundProcess process =
                _backgroundProcessFactory.Create(OrbitBinaryPath, args, SDKUtil.GetOrbitPath());
            process.Start();
        }

        public bool IsOrbitInstalled()
        {
            return _fileSystem.File.Exists(OrbitBinaryPath);
        }
    }
}
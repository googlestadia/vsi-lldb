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

namespace YetiVSI.Profiling
{
    public interface IProfilerArgs
    {
        string Args { get; }
    }

    public class OrbitArgs : IProfilerArgs
    {
        public string Args { get; }

        /// <param name="gameletExecutablePath">Full path to the game binary</param>
        /// <param name="gameletId">ID of the gamelet that runs the binary</param>
        public OrbitArgs(string gameletExecutablePath, string gameletId)
        {
            Args = $"--target_process={gameletExecutablePath} --target_instance={gameletId} " +
                "--launched_from_vsi";
        }
    }

    public class DiveArgs : IProfilerArgs
    {
        public string Args => string.Empty;
    }

    public interface IProfilerLauncher<in TArgs> where TArgs : IProfilerArgs
    {
        /// <summary>
        /// Returns the full path of the profiler binary.
        /// </summary>
        string BinaryPath { get; }

        /// <summary>
        /// Returns true if the profiler component was installed during SDK installation.
        /// </summary>
        bool IsInstalled { get; }

        /// <summary>
        /// Launches the profiler with the given arguments.
        /// </summary>
        /// <exception cref="ProcessException">If the profiler failed to launch</exception>
        void Launch(TArgs args);
    }

    public class ProfilerLauncher<TArgs> : IProfilerLauncher<TArgs> where TArgs : IProfilerArgs
    {
        // Creates an Orbit launcher.
        public static ProfilerLauncher<OrbitArgs> CreateForOrbit(
            BackgroundProcess.Factory backgroundProcessFactory, IFileSystem fileSystem) =>
            new ProfilerLauncher<OrbitArgs>(SDKUtil.GetOrbitPath(), "Orbit.exe",
                                            backgroundProcessFactory, fileSystem);

        // Creates an Dive launcher.
        public static ProfilerLauncher<DiveArgs> CreateForDive(
            BackgroundProcess.Factory backgroundProcessFactory, IFileSystem fileSystem) =>
            new ProfilerLauncher<DiveArgs>(SDKUtil.GetDivePath(), "dive.exe",
                                           backgroundProcessFactory, fileSystem);

        public string BinaryPath { get; }
        public bool IsInstalled => _fileSystem.File.Exists(BinaryPath);

        readonly string _profilerDir;
        readonly IFileSystem _fileSystem;
        readonly BackgroundProcess.Factory _backgroundProcessFactory;

        ProfilerLauncher(string profilerDir, string profilerFileName,
                         BackgroundProcess.Factory backgroundProcessFactory, IFileSystem fileSystem)
        {
            _profilerDir = profilerDir;
            BinaryPath = Path.Combine(_profilerDir, profilerFileName);
            _backgroundProcessFactory = backgroundProcessFactory;
            _fileSystem = fileSystem;
        }

        public void Launch(TArgs args)
        {
            IBackgroundProcess process =
                _backgroundProcessFactory.Create(BinaryPath, args.Args, _profilerDir);
            process.Start();
        }
    }
}
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

using System;
using System.IO;
using System.Threading.Tasks;

namespace YetiCommon
{
    public interface IModuleParser
    {
        /// <summary>
        /// Parse a <see cref="DebugLinkLocationInfo"/> instance from the file and populate its
        /// Error property with all warnings that occurred during the processing. This method
        /// doesn't throw, all errors will be returned in the Error property.
        /// </summary>
        /// <param name="filepath">Full path to the file to parse.</param>
        /// <returns>Debug link information, optionally containing Filename, Directory, and
        /// Error.</returns>
        DebugLinkLocationInfo ParseDebugLinkInfo(string filepath);
        /// <summary>
        /// Parse a <see cref="BuildIdInfo"/> instance from the  file and populate its Error
        /// property with all warnings that  occurred during the processing. This method doesn't
        /// throw, all errors will be returned in the Error property.
        /// </summary>
        /// <param name="filepath">Full path to the file to parse.</param>
        /// <param name="format">Expected format of the file to parse.</param>
        /// <returns>Build ID info instance, optionally containing BuildId, and Error.</returns>
        BuildIdInfo ParseBuildIdInfo(string filepath, ModuleFormat format);

        /// <summary>
        /// Parses a binary or symbol file on a gamelet and returns the build ID encoded in the
        /// .note.gnu.build-id section of the file.
        /// </summary>
        /// <param name="filepath">The remote absolute file path.</param>
        /// <param name="target">The remote gamelet.</param>
        /// <returns>A non-empty build id.</returns>
        /// <exception cref="BinaryFileUtilException">
        /// Thrown when an error is encountered reading or parsing the build id.
        /// InnerException contains more details.
        /// </exception>
        /// <exception cref="InvalidBuildIdException">
        /// Thrown when the build id has an unexpected format.
        /// </exception>
        Task<BuildId> ParseRemoteBuildIdInfoAsync(string filepath, SSH.SshTarget target);

        /// <summary>
        /// Checks whether file is a valid ELF file.
        /// </summary>
        /// <param name="filepath">Full path to the file to validate.</param>
        /// <param name="isDebugInfoFile">In addition to parsing the file, also check if BuildId
        /// is populated.</param>
        /// <param name="errorMessage">Generated error message.</param>
        bool IsValidElf(string filepath, bool isDebugInfoFile, out string errorMessage);
    }

    public class DebugLinkLocationInfo
    {
        public DebugLinkLocation Data { get; set; } = new DebugLinkLocation();

        public void SetFilename(string filename)
        {
            Data.Filename = filename;
        }

        public void SetDirectory(string directory)
        {
            Data.Directory = directory;
        }

        string _error = "";
        public string Error
        {
            get => _error.Trim();
            private set => _error = value;
        }

        public void AddError(string message)
        {
            Error += $"{message}{Environment.NewLine}";
        }

        public bool HasError => !string.IsNullOrWhiteSpace(Error);
    }

    public class DebugLinkLocation
    {
        public string Filename { get; set; } = "";
        public string Directory { get; set; } = "";

        public bool TryGetFullPath(out string fullPath)
        {
            if (!string.IsNullOrWhiteSpace(Filename) && !string.IsNullOrWhiteSpace(Directory))
            {
                fullPath = Path.Combine(Directory, Filename);
                return true;
            }

            fullPath = "";
            return false;
        }
    }

    public class BuildIdInfo
    {
        public BuildId Data { get; set; }
        string _error = "";
        public string Error
        {
            get => _error.Trim();
            private set => _error = value;
        }

        public void AddError(string message)
        {
            Error += $"{message}{Environment.NewLine}";
        }

        public bool HasError => !string.IsNullOrWhiteSpace(Error);
    }
}
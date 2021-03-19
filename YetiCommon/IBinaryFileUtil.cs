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
using System.Threading.Tasks;

namespace YetiCommon
{
    /// <summary>
    /// Utility for extracting info from binary and symbol files.
    ///
    /// TODO: Make interface async
    /// </summary>
    public interface IBinaryFileUtil
    {
        /// <summary>
        /// Parses a binary or symbol file and returns a BuildId that uniquely identifies that
        /// specific build of the binary and its symbol file.
        /// </summary>
        ///
        /// <exception cref="BinaryFileUtilException">
        /// Thrown on failure, including when the file cannot be found or
        /// the build ID cannot be extracted. If there was an error executing the tool used to
        /// extract information, the InnerException will be a ProcessException.
        /// </exception>
        /// <exception cref="InvalidBuildIdException">
        /// Thrown on failure to find or parse a build ID of an otherwise valid binary, including
        /// the case when the build id has length zero.
        /// </exception>
        BuildId ReadBuildId(string filepath, SSH.SshTarget target = null);

        /// <summary>
        /// Async version of ReadBuildId
        /// </summary>
        Task<BuildId> ReadBuildIdAsync(string filepath, SSH.SshTarget target = null);

        /// <summary>
        /// Parses a binary file and returns the name of the matching symbol file.
        /// </summary>
        ///
        /// <exception cref="BinaryFileUtilException">
        /// Thrown on failure, including when the file cannot be found or
        /// the symbol file name cannot be extracted. If there was an error executing the tool used
        /// to extract information, the InnerException will be a ProcessException.
        /// </exception>
        string ReadSymbolFileName(string filepath);

        /// <summary>
        /// Parses a binary file and returns the directory of the debug symbol file.
        /// </summary>
        ///
        /// <exception cref="BinaryFileUtilException">
        /// Thrown on failure, including when the file cannot be found or the symbol file directory
        /// cannot be extracted. If there was an error executing the tool used to extract
        /// information, the InnerException will be a ProcessException.
        /// </exception>
        string ReadSymbolFileDir(string filepath);

        /// <summary>
        /// Verifies that the symbol file is a valid elf file. Optionally, it checks if the file
        /// contains .debug_info section.
        /// </summary>
        ///
        /// <exception cref="BinaryFileUtilException">
        /// Thrown on verification failure, including when the file cannot be found or if it is not
        /// en elf file or if no .debug_info section was find. If there was an error executing the
        /// tool used to extract information, the InnerException will be a ProcessException.
        /// </exception>
        void VerifySymbolFile(string filepath, bool isDebugInfoFile);
    }

    public class BinaryFileUtilException : Exception, IUserVisibleError
    {
        public string UserDetails { get { return ToString(); } }

        public BinaryFileUtilException(string message) : base(message) { }

        public BinaryFileUtilException(string message, Exception e) : base(message, e) { }
    }

    public class InvalidBuildIdException : BinaryFileUtilException, IConfigurationError
    {
        public InvalidBuildIdException(string message) : base(message) { }

        public InvalidBuildIdException(string message, Exception e) : base(message, e) { }
    }
}

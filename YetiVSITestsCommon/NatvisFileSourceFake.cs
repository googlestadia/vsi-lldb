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

﻿using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using YetiVSI.DebugEngine.NatvisEngine;

namespace YetiVSITestsCommon
{
    /// <summary>
    /// Fake natvis file source to be used for tests.
    /// </summary>
    public class NatvisFileSourceFake : INatvisFileSource
    {
        readonly IFileSystem _fileSystem;
        readonly string _path;

        /// <param name="path">Path, can be a directory or a file path.</param>
        /// <remarks>If path is a directory then files matching *.natvis in the directory and all
        /// subdirectories are returned.</remarks>
        public NatvisFileSourceFake(IFileSystem fileSystem, string path)
        {
            _fileSystem = fileSystem;
            _path = path;
        }

        #region INatvisFileSource

        public IEnumerable<string> GetFilePaths()
        {
            if (_fileSystem.File.Exists(_path))
            {
                return new string[] { _path };
            }

            if (_fileSystem.Directory.Exists(_path))
            {
                return _fileSystem.Directory.EnumerateFiles(
                    _path, "*.natvis", SearchOption.AllDirectories);
            }

            Trace.WriteLine($"ERROR: File path is neither a file nor a directory: '{_path}'");
            return Enumerable.Empty<string>();
        }

        #endregion
    }
}
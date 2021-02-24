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

﻿using System.IO.Abstractions;
using TestsCommon.TestSupport;
using YetiCommon;
using YetiCommon.SSH;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SymbolStores.Tests
{
    class FakeBinaryFileUtil : IBinaryFileUtil
    {
        IFileSystem _fileSystem;
        Dictionary<string, string> _verificationFailures = new Dictionary<string, string>();

        public FakeBinaryFileUtil(IFileSystem fileSystem)
        {
            this._fileSystem = fileSystem;
        }

        public void AddVerificationFailureFor(BuildId buildId, string errorString)
        {
            _verificationFailures.Add(buildId.ToString(), errorString);
        }

        // Instead of properly parsing the file, just read its entire contents as binary and
        // return that as the build ID
        public BuildId ReadBuildId(string filepath, SshTarget target = null)
        {
            return new BuildId(_fileSystem.File.ReadAllText(filepath));
        }

        public async Task<BuildId> ReadBuildIdAsync(string filepath, SshTarget target = null)
        {
            BuildId buildID = ReadBuildId(filepath);
            return await Task.FromResult(buildID);
        }

        public string ReadSymbolFileName(string filepath)
        {
            throw new NotImplementedTestDoubleException();
        }

        public string ReadSymbolFileDir(string filepath)
        {
            throw new NotImplementedTestDoubleException();
        }

        public void VerifySymbolFile(string filepath, bool isDebugInfoFile)
        {
            BuildId buildId = ReadBuildId(filepath);
            if (_verificationFailures.TryGetValue(buildId.ToString(), out string errorString))
            {
                throw new BinaryFileUtilException(errorString);
            }
        }
    }
}

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

using System.IO.Abstractions;
using System.Threading.Tasks;
using YetiCommon;
using YetiCommon.SSH;

namespace SymbolStores.Tests
{
    class FakeBinaryFileUtil : IBinaryFileUtil
    {
        readonly IFileSystem _fileSystem;

        public FakeBinaryFileUtil(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        // Instead of properly parsing the file, just read its entire contents as binary and
        // return that as the build ID
        public async Task<BuildId> ReadBuildIdAsync(string filepath, SshTarget target = null)
        {
            BuildId buildID = new BuildId(_fileSystem.File.ReadAllText(filepath));
            return await Task.FromResult(buildID);
        }
    }
}

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

using System.IO;
using System.IO.Abstractions;
using YetiCommon;

namespace SymbolStores.Tests
{
    class FakeBuildIdWriter
    {
        readonly IFileSystem _fileSystem;

        public FakeBuildIdWriter(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        // Just set the entire contents of the file to the bytes of the build ID
        public void WriteBuildId(string filepath, BuildId buildId)
        {
            _fileSystem.Directory.CreateDirectory(Path.GetDirectoryName(filepath));
            _fileSystem.File.WriteAllText(filepath, buildId.ToHexString());
        }
    }
}

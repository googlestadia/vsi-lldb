// Copyright 2021 Google LLC
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
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using YetiCommon;
using YetiCommon.Logging;

namespace SymbolStores
{
    /// <summary>
    /// Interface that allows FlatSymbolStore to be mocked in tests
    /// </summary>
    public interface IFlatSymbolStore : ISymbolStore
    {
    }

    /// <summary>
    /// Represents a flat directory containing symbol files
    /// </summary>
    public class FlatSymbolStore : SymbolStoreBase, IFlatSymbolStore
    {
        readonly IFileSystem _fileSystem;
        readonly IModuleParser _moduleParser;
        [JsonProperty("Path")]
        readonly string _path;

        public static bool IsFlatStore(IFileSystem fileSystem, string path) =>
            !string.IsNullOrWhiteSpace(path)
            && !fileSystem.Path.GetInvalidPathChars().Any(x => path.Contains(x));

        public FlatSymbolStore(IFileSystem fileSystem, IModuleParser moduleParser, string path)
            : base(false, false)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException(
                    Strings.FailedToCreateFlatStore(Strings.PathNullOrEmpty));
            }

            _fileSystem = fileSystem;
            _moduleParser = moduleParser;
            _path = fileSystem.Path.GetFullPath(path);
        }

        #region SymbolStoreBase functions

        public override Task<IFileReference> FindFileAsync(string filename, BuildId buildId,
                                                           bool isDebugInfoFile,
                                                           TextWriter log,
                                                           bool forceLoad)
        {
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentException(Strings.FilenameNullOrEmpty, nameof(filename));
            }

            string filepath;
            try
            {
                filepath = Path.Combine(_path, filename);
            }
            catch (ArgumentException e)
            {
                log.WriteLineAndTrace(
                    Strings.FailedToSearchFlatStore(_path, filename, e.Message));
                return Task.FromResult<IFileReference>(null);
            }

            if (!_fileSystem.File.Exists(filepath))
            {
                log.WriteLineAndTrace(Strings.FileNotFound(filepath));
                return Task.FromResult<IFileReference>(null); 
            }

            if (buildId != BuildId.Empty)
            {
                // TODO: pass correct isElf value
                BuildIdInfo actualBuildId = _moduleParser.ParseBuildIdInfo(filepath, true);

                if (actualBuildId.HasError)
                {
                    log.WriteLineAndTrace(actualBuildId.Error);
                    return Task.FromResult<IFileReference>(null);
                }

                if (actualBuildId.Data != buildId)
                {
                    log.WriteLineAndTrace(
                        Strings.BuildIdMismatch(filepath, buildId, actualBuildId.Data));
                    return Task.FromResult<IFileReference>(null);
                }
            }

            log.WriteLineAndTrace(Strings.FileFound(filepath));
            return Task.FromResult<IFileReference>(new FileReference(_fileSystem, filepath));
        }

        public override Task<IFileReference> AddFileAsync(IFileReference source, string filename,
                                                          BuildId buildId, TextWriter log) =>
            throw new NotSupportedException(Strings.CopyToFlatStoreNotSupported);

        public override bool DeepEquals(ISymbolStore otherStore) =>
            otherStore is FlatSymbolStore other && _path == other._path;

        #endregion
    }
}

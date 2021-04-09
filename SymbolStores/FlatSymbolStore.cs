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

﻿using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Threading.Tasks;
using YetiCommon;

namespace SymbolStores
{
    /// <summary>
    /// Interface that allows FlatSymbolStore to be mocked in tests
    /// </summary>
    public interface IFlatSymbolStore : ISymbolStore { }

    /// <summary>
    /// Represents a flat directory containing symbol files
    /// </summary>
    public class FlatSymbolStore : SymbolStoreBase, IFlatSymbolStore
    {
        public class Factory
        {
            readonly IFileSystem _fileSystem;
            readonly IBinaryFileUtil _binaryFileUtil;
            readonly FileReference.Factory _symbolFileFactory;

            public Factory(IFileSystem fileSystem, IBinaryFileUtil binaryFileUtil,
                FileReference.Factory symbolFileFactory)
            {
                _fileSystem = fileSystem;
                _binaryFileUtil = binaryFileUtil;
                _symbolFileFactory = symbolFileFactory;
            }

            // Throws ArgumentException if path is null or empty
            public virtual IFlatSymbolStore Create(string path) => new FlatSymbolStore(
                _fileSystem, _binaryFileUtil, _symbolFileFactory, path);
        }

        readonly IFileSystem _fileSystem;
        readonly IBinaryFileUtil _binaryFileUtil;
        readonly FileReference.Factory _symbolFileFactory;
        [JsonProperty("Path")]
        string _path;

        FlatSymbolStore(IFileSystem fileSystem, IBinaryFileUtil binaryFileUtil,
            FileReference.Factory symbolFileFactory, string path)
            : base(false, false)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException(
                    Strings.FailedToCreateFlatStore(Strings.PathNullOrEmpty));
            }

            _fileSystem = fileSystem;
            _binaryFileUtil = binaryFileUtil;
            _symbolFileFactory = symbolFileFactory;
            _path = path;
        }

        #region SymbolStoreBase functions

        public override async Task<IFileReference> FindFileAsync(
            string filename, BuildId buildId, bool isDebugInfoFile, TextWriter log)
        {
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentException(Strings.FilenameNullOrEmpty, "filename");
            }

            string filepath;

            try
            {
                filepath = Path.Combine(_path, filename);
            }
            catch (ArgumentException e)
            {
                Trace.WriteLine(Strings.FailedToSearchFlatStore(_path, filename, e.Message));
                await log.WriteLineAsync(
                    Strings.FailedToSearchFlatStore(_path, filename, e.Message));
                return null;
            }
            if (!_fileSystem.File.Exists(filepath))
            {
                Trace.WriteLine(Strings.FileNotFound(filepath));
                await log.WriteLineAsync(Strings.FileNotFound(filepath));
                return null;
            }
            if (buildId != BuildId.Empty)
            {
                try
                {
                    BuildId actualBuildId = await _binaryFileUtil.ReadBuildIdAsync(filepath);
                    if (actualBuildId != buildId)
                    {
                        Trace.WriteLine(Strings.BuildIdMismatch(filepath, buildId, actualBuildId));
                        await log.WriteLineAsync(
                            Strings.BuildIdMismatch(filepath, buildId, actualBuildId));
                        return null;
                    }
                }
                catch (BinaryFileUtilException e)
                {
                    Trace.WriteLine(e.ToString());
                    await log.WriteLineAsync(e.Message);
                    return null;
                }
            }

            Trace.WriteLine(Strings.FileFound(filepath));
            await log.WriteLineAsync(Strings.FileFound(filepath));
            return _symbolFileFactory.Create(filepath);
        }

        public override Task<IFileReference> AddFileAsync(IFileReference source, string filename,
                                                          BuildId buildId, TextWriter log) =>
            throw new NotSupportedException(Strings.CopyToFlatStoreNotSupported);

        public override bool DeepEquals(ISymbolStore otherStore) =>
            otherStore is FlatSymbolStore other && _path == other._path;

#endregion
    }
}

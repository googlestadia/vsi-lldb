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
using System.IO;
using System.IO.Abstractions;
using System.Threading.Tasks;
using YetiCommon;
using YetiCommon.Logging;

namespace SymbolStores
{
    /// <summary>
    /// Interface that allows StructuredSymbolStore to be mocked in tests
    /// </summary>
    public interface IStructuredSymbolStore : ISymbolStore
    {
    }

    /// <summary>
    /// Represents a file system location where symbol files are stored, with the directory
    /// structure "path/symbolFileName/buildId/symbolFile".
    /// A structured store can be disambiguated from a flat store by the existence of an empty
    /// marker file named "pingme.txt".
    /// </summary>
    public class StructuredSymbolStore : SymbolStoreBase, IStructuredSymbolStore
    {
        // Structured symbol stores can be identified by the existence of a marker file
        public static bool IsStructuredStore(IFileSystem fileSystem, string path) =>
            fileSystem.File.Exists(Path.Combine(path, _markerFileName));

        const string _markerFileName = "pingme.txt";

        readonly IFileSystem _fileSystem;

        [JsonProperty("Path")]
        readonly string _path;

        public StructuredSymbolStore(IFileSystem fileSystem, string path, bool isCache = false)
            : base(true, isCache)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException(
                    Strings.FailedToCreateStructuredStore(Strings.PathNullOrEmpty));
            }

            _fileSystem = fileSystem;
            _path = path;
        }

#region SymbolStoreBase functions

        public override async Task<IFileReference> FindFileAsync(string filename, BuildId buildId,
                                                                 bool isDebugInfoFile,
                                                                 TextWriter log)
        {
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentException(Strings.FilenameNullOrEmpty, "filename");
            }
            if (buildId == BuildId.Empty)
            {
                await log.WriteLogAsync(
                    Strings.FailedToSearchStructuredStore(_path, filename, Strings.EmptyBuildId));
                return null;
            }

            string filepath;

            try
            {
                filepath = Path.Combine(_path, filename, buildId.ToString(), filename);
            }
            catch (ArgumentException e)
            {
                await log.WriteLogAsync(
                    Strings.FailedToSearchStructuredStore(_path, filename, e.Message));
                return null;
            }
            if (!_fileSystem.File.Exists(filepath))
            {
                await log.WriteLogAsync(Strings.FileNotFound(filepath));
                return null;
            }

            await log.WriteLogAsync(Strings.FileFound(filepath));
            return new FileReference(_fileSystem, filepath);
        }

        public override async Task<IFileReference> AddFileAsync(IFileReference source,
                                                                string filename, BuildId buildId,
                                                                TextWriter log)
        {
            if (source == null)
            {
                throw new ArgumentException(Strings.FailedToCopyToStructuredStore(
                                                _path, filename, Strings.SourceFileReferenceNull),
                                            nameof(source));
            }
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentException(Strings.FailedToCopyToStructuredStore(
                                                _path, filename, Strings.FilenameNullOrEmpty),
                                            nameof(filename));
            }
            if (buildId == BuildId.Empty)
            {
                throw new ArgumentException(
                    Strings.FailedToCopyToStructuredStore(_path, filename, Strings.EmptyBuildId),
                    nameof(buildId));
            }

            try
            {
                AddMarkerFileIfNeeded();

                string filepath = Path.Combine(_path, filename, buildId.ToString(), filename);
                await source.CopyToAsync(filepath);

                await log.WriteLogAsync(Strings.CopiedFile(filename, filepath));

                return new FileReference(_fileSystem, filepath);
            }
            catch (Exception e) when (e is SymbolStoreException || e is IOException ||
                                      e is UnauthorizedAccessException ||
                                      e is NotSupportedException || e is ArgumentException)
            {
                throw new SymbolStoreException(
                    Strings.FailedToCopyToStructuredStore(_path, filename, e.Message), e);
            }
        }

        public override bool DeepEquals(ISymbolStore otherStore) =>
            otherStore is StructuredSymbolStore other && IsCache == other.IsCache
            && _path == other._path;

#endregion

        public void AddMarkerFileIfNeeded()
        {
            string markerFilePath = Path.Combine(_path, _markerFileName);
            if (_fileSystem.File.Exists(markerFilePath))
            {
                return;
            }

            _fileSystem.Directory.CreateDirectory(_path);
            _fileSystem.File.Create(markerFilePath).Close();
        }
    }
}

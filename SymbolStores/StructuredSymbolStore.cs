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
    /// Interface that allows StructuredSymbolStore to be mocked in tests
    /// </summary>
    public interface IStructuredSymbolStore : ISymbolStore { }


    /// <summary>
    /// Represents a file system location where symbol files are stored, with the directory
    /// structure "path/symbolFileName/buildId/symbolFile".
    /// A structured store can be disambiguated from a flat store by the existence of an empty
    /// marker file named "pingme.txt".
    /// </summary>
    public class StructuredSymbolStore : SymbolStoreBase, IStructuredSymbolStore
    {
        public class Factory
        {
            IFileSystem fileSystem;
            FileReference.Factory fileReferenceFactory;

            public Factory(IFileSystem fileSystem, FileReference.Factory fileReferenceFactory)
            {
                this.fileSystem = fileSystem;
                this.fileReferenceFactory = fileReferenceFactory;
            }

            // Throws ArgumentException if path is null or empty
            public virtual IStructuredSymbolStore Create(string path, bool isCache = false,
                bool shouldInitialize = false)
            {
                return new StructuredSymbolStore(fileSystem, fileReferenceFactory, path, isCache,
                    shouldInitialize);
            }
        }

        // Structured symbol stores can be identified by the existance of a marker file
        public static bool IsStructuredStore(IFileSystem fileSystem, string path)
        {
            return fileSystem.File.Exists(Path.Combine(path, MARKER_FILE_NAME));
        }

        const string MARKER_FILE_NAME = "pingme.txt";

        IFileSystem fileSystem;
        FileReference.Factory fileReferenceFactory;

        [JsonProperty("Path")]
        string path;

        StructuredSymbolStore(IFileSystem fileSystem, FileReference.Factory fileReferenceFactory,
            string path, bool isCache, bool shouldInitialize) : base(true, isCache)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException(
                    Strings.FailedToCreateStructuredStore(Strings.PathNullOrEmpty));
            }

            this.fileSystem = fileSystem;
            this.fileReferenceFactory = fileReferenceFactory;
            this.path = path;

            if (shouldInitialize)
            {
                AddMarkerFileIfNeeded();
            }
        }

        #region SymbolStoreBase functions

        public override async Task<IFileReference> FindFileAsync(
            string filename, BuildId buildId, bool isDebugInfoFile, TextWriter log)
        {
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentException(Strings.FilenameNullOrEmpty, "filename");
            }
            if (buildId == BuildId.Empty)
            {
                Trace.WriteLine(
                    Strings.FailedToSearchStructuredStore(path, filename, Strings.EmptyBuildId));
                await log.WriteLineAsync(
                    Strings.FailedToSearchStructuredStore(path, filename, Strings.EmptyBuildId));
                return null;
            }

            string filepath;

            try
            {
                filepath = Path.Combine(path, filename, buildId.ToString(), filename);
            }
            catch (ArgumentException e)
            {
                Trace.WriteLine(Strings.FailedToSearchStructuredStore(path, filename, e.Message));
                await log.WriteLineAsync(Strings.FailedToSearchStructuredStore(path, filename, e.Message));
                return null;
            }
            if (!fileSystem.File.Exists(filepath))
            {
                Trace.WriteLine(Strings.FileNotFound(filepath));
                await log.WriteLineAsync(Strings.FileNotFound(filepath));
                return null;
            }

            Trace.WriteLine(Strings.FileFound(filepath));
            await log.WriteLineAsync(Strings.FileFound(filepath));
            return fileReferenceFactory.Create(filepath);
        }

        public override async Task<IFileReference> AddFileAsync(
            IFileReference source, string filename, BuildId buildId, TextWriter log)
        {
            if (source == null)
            {
                throw new ArgumentException(Strings.FailedToCopyToStructuredStore(path, filename,
                    Strings.SourceFileReferenceNull), "source");
            }
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentException(Strings.FailedToCopyToStructuredStore(path, filename,
                    Strings.FilenameNullOrEmpty), "filename");
            }
            if (buildId == BuildId.Empty)
            {
                throw new ArgumentException(Strings.FailedToCopyToStructuredStore(path, filename,
                    Strings.EmptyBuildId), "buildId");
            }

            try
            {
                AddMarkerFileIfNeeded();

                var filepath = Path.Combine(path, filename, buildId.ToString(), filename);
                await source.CopyToAsync(filepath);

                Trace.WriteLine(Strings.CopiedFile(filename, filepath));
#pragma warning disable VSTHRD103
                log.WriteLine(Strings.CopiedFile(filename, filepath));
#pragma warning restore VSTHRD103

                return fileReferenceFactory.Create(filepath);
            }
            catch (Exception e) when (e is SymbolStoreException || e is IOException ||
                e is UnauthorizedAccessException || e is NotSupportedException ||
                e is ArgumentException)
            {
                throw new SymbolStoreException(
                    Strings.FailedToCopyToStructuredStore(path, filename, e.Message), e);
            }
        }

        public override bool DeepEquals(ISymbolStore otherStore)
        {
            var other = otherStore as StructuredSymbolStore;
            return other != null && IsCache == other.IsCache && path == other.path;
        }

        #endregion

        public void AddMarkerFileIfNeeded()
        {
            if (!fileSystem.File.Exists(Path.Combine(path, MARKER_FILE_NAME)))
            {
                fileSystem.Directory.CreateDirectory(path);
                fileSystem.File.Create(Path.Combine(path, MARKER_FILE_NAME)).Close();
            }
        }
    }
}

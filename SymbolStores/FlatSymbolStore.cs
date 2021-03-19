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
            IFileSystem fileSystem;
            IBinaryFileUtil binaryFileUtil;
            FileReference.Factory symbolFileFactory;

            public Factory(IFileSystem fileSystem, IBinaryFileUtil binaryFileUtil,
                FileReference.Factory symbolFileFactory)
            {
                this.fileSystem = fileSystem;
                this.binaryFileUtil = binaryFileUtil;
                this.symbolFileFactory = symbolFileFactory;
            }

            // Throws ArgumentException if path is null or empty
            public virtual IFlatSymbolStore Create(string path)
            {
                return new FlatSymbolStore(fileSystem, binaryFileUtil, symbolFileFactory, path);
            }
        }

        IFileSystem fileSystem;
        IBinaryFileUtil binaryFileUtil;
        FileReference.Factory symbolFileFactory;
        [JsonProperty("Path")]
        string path;

        FlatSymbolStore(IFileSystem fileSystem, IBinaryFileUtil binaryFileUtil,
            FileReference.Factory symbolFileFactory, string path)
            : base(false, false)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException(
                    Strings.FailedToCreateFlatStore(Strings.PathNullOrEmpty));
            }

            this.fileSystem = fileSystem;
            this.binaryFileUtil = binaryFileUtil;
            this.symbolFileFactory = symbolFileFactory;
            this.path = path;
        }

        #region SymbolStoreBase functions

        public override IFileReference FindFile(string filename, BuildId buildId,
                                                bool isDebugInfoFile, TextWriter log)
        {
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentException(Strings.FilenameNullOrEmpty, "filename");
            }

            string filepath;

            try
            {
                filepath = Path.Combine(path, filename);
            }
            catch (ArgumentException e)
            {
                Trace.WriteLine(Strings.FailedToSearchFlatStore(path, filename, e.Message));
                log.WriteLine(Strings.FailedToSearchFlatStore(path, filename, e.Message));
                return null;
            }
            if (!fileSystem.File.Exists(filepath))
            {
                Trace.WriteLine(Strings.FileNotFound(filepath));
                log.WriteLine(Strings.FileNotFound(filepath));
                return null;
            }
            if (buildId != BuildId.Empty)
            {
                try
                {
                    var actualBuildId = binaryFileUtil.ReadBuildId(filepath);
                    if (actualBuildId != buildId)
                    {
                        Trace.WriteLine(Strings.BuildIdMismatch(filepath, buildId, actualBuildId));
                        log.WriteLine(Strings.BuildIdMismatch(filepath, buildId, actualBuildId));
                        return null;
                    }
                }
                catch (BinaryFileUtilException e)
                {
                    Trace.WriteLine(e.ToString());
                    log.WriteLine(e.Message);
                    return null;
                }
            }

            Trace.WriteLine(Strings.FileFound(filepath));
            log.WriteLine(Strings.FileFound(filepath));
            return symbolFileFactory.Create(filepath);
        }

        public override IFileReference AddFile(IFileReference source, string filename,
            BuildId buildId, TextWriter log)
        {
            throw new NotSupportedException(Strings.CopyToFlatStoreNotSupported);
        }

        public override bool DeepEquals(ISymbolStore otherStore)
        {
            var other = otherStore as FlatSymbolStore;
            return other != null && path == other.path;
        }

        #endregion
    }
}

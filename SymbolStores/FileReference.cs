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
using System.IO;
using System.IO.Abstractions;
using System.Threading.Tasks;

namespace SymbolStores
{
    // Represents a file that exists at a filesystem path.
    public sealed class FileReference : IFileReference
    {
        public class Factory
        {
            IFileSystem fileSystem;

            public Factory(IFileSystem fileSystem)
            {
                this.fileSystem = fileSystem;
            }

            public FileReference Create(string filepath)
            {
                if (filepath == null)
                {
                    throw new ArgumentNullException(nameof(filepath));
                }

                return new FileReference(fileSystem, filepath);
            }
        }

        IFileSystem fileSystem;

        FileReference(IFileSystem fileSystem, string filepath)
        {
            this.fileSystem = fileSystem;
            Location = filepath;
        }

        #region ISymbolFile functions

        public bool IsFilesystemLocation => true;

        public string Location { get; private set; }

        public Task CopyToAsync(string destFilepath)
        {
            if (destFilepath == null)
            {
                throw new ArgumentNullException(nameof(destFilepath));
            }

            try
            {
                // Delete the file if it already exists.
                if (fileSystem.File.Exists(destFilepath))
                {
                    fileSystem.File.Delete(destFilepath);
                }

                var destDirectory = Path.GetDirectoryName(destFilepath);
                // If destFilePath does not have directory information (eg. if it's a relative path
                // to a file in the current directory) GetDirectoryName will return an empty
                // string.
                if (!string.IsNullOrEmpty(destDirectory))
                {
                    fileSystem.Directory.CreateDirectory(destDirectory);
                }

                // Copy to a temp file and rename in order to avoid potentially leaving a
                // half-copied file in the case of an event such as a power failure.
                // TODO: Potentially consolidate the temp files in a hidden folder
                // at the root of the destination store.
                string tempFilepath = $"{destFilepath}.{Guid.NewGuid()}.temp";
                try
                {
                    fileSystem.File.Copy(Location, tempFilepath);
                    fileSystem.File.Move(tempFilepath, destFilepath);
                }
                finally
                {
                    fileSystem.File.Delete(tempFilepath);
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException ||
                e is NotSupportedException || e is ArgumentException)
            {
                throw new SymbolStoreException(e.Message, e);
            }

            return Task.CompletedTask;
        }

        #endregion
    }
}

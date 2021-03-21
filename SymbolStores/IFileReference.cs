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

using System.Threading.Tasks;

namespace SymbolStores
{
    // Represents a file that exists at a particular location, which may or may not be a filesystem
    // path.
    public interface IFileReference
    {
        // Copies the file to the specified file path.
        // Creates the destination directory if it does not already exist.
        // Throws a SymbolStoreException on failure.
        Task CopyToAsync(string destFilepath);

        // The path of the file if the file is accessible through the filesystem, otherwise the
        // file's uri.
        string Location { get; }

        // Whether or not the file is located at a filesystem path.
        bool IsFilesystemLocation { get; }
    }
}

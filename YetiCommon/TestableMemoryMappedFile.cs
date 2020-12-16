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

using System;
using System.IO.MemoryMappedFiles;

namespace YetiCommon
{
    // A factory used to create memory mapped files.  This wraps the static library calls to make
    // it testable.
    public class MemoryMappedFileFactory
    {
        public virtual IMemoryMappedFile CreateNew(string mapName, long capacity)
        {
            var mappedFile = MemoryMappedFile.CreateNew(mapName, capacity);
            return new TestableMemoryMappedFile(mappedFile);
        }
    }

    public interface IMemoryMappedFile : IDisposable
    {
    }

    // Wrap the MemoryMappedFile object to make it testable (since it doesn't have any public
    // constructors, the MemoryMappedFile class is not mockable).
    public class TestableMemoryMappedFile : IMemoryMappedFile
    {
        bool disposed = false;
        MemoryMappedFile memoryMappedFile;

        public TestableMemoryMappedFile(MemoryMappedFile memoryMappedFile)
        {
            this.memoryMappedFile = memoryMappedFile;
        }

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                memoryMappedFile.Dispose();
                memoryMappedFile = null;
            }
            disposed = true;
        }

        #endregion
    }
}

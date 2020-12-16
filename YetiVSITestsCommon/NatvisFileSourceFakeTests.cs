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

﻿using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;

namespace YetiVSITestsCommon
{
    [TestFixture]
    public class NatvisFileSourceFakeTests
    {
        MockFileSystem _fileSystem;

        [SetUp]
        public void SetUp()
        {
            _fileSystem = new MockFileSystem();
        }

        [Test]
        public void PathIsAFile()
        {
            string dir = Path.Combine(@"C:\", "project_root", "natvis_dir");
            string filePath1 = Path.Combine(dir, "file1.natvis");
            string filePath2 = Path.Combine(dir, "file2.natvis");

            _fileSystem.AddDirectory(dir);
            _fileSystem.AddFile(filePath1, MockFileData.NullObject);
            _fileSystem.AddFile(filePath2, MockFileData.NullObject);

            var fileSource = new NatvisFileSourceFake(_fileSystem, filePath1);

            IEnumerable<string> paths = fileSource.GetFilePaths();

            Assert.That(paths, Is.EquivalentTo(new [] { filePath1 }));
        }

        [Test]
        public void PathIsADirectory()
        {
            string dir = Path.Combine(@"C:\", "project_root", "natvis_dir");
            string subDir = Path.Combine(dir, "subdir");
            string filePath1 = Path.Combine(dir, "file1.natvis");
            string filePath2 = Path.Combine(dir, "file2.natvis");
            string filePath3 = Path.Combine(subDir, "file3.natvis");

            _fileSystem.AddDirectory(dir);
            _fileSystem.AddDirectory(subDir);
            _fileSystem.AddFile(filePath1, MockFileData.NullObject);
            _fileSystem.AddFile(filePath2, MockFileData.NullObject);
            _fileSystem.AddFile(filePath3, MockFileData.NullObject);

            var fileSource = new NatvisFileSourceFake(_fileSystem, dir);

            IEnumerable<string> paths = fileSource.GetFilePaths();

            Assert.That(paths, Is.EquivalentTo(new [] { filePath1, filePath2, filePath3 }));
        }

        [Test]
        public void FiltersBasedOnFileExtension()
        {
            string dir = Path.Combine(@"C:\", "project_root", "natvis_dir");
            string filePath1 = Path.Combine(dir, "file1.natvis");
            string filePath2 = Path.Combine(dir, "file2.txt");

            _fileSystem.AddDirectory(dir);
            _fileSystem.AddFile(filePath1, MockFileData.NullObject);
            _fileSystem.AddFile(filePath2, MockFileData.NullObject);

            var fileSource = new NatvisFileSourceFake(_fileSystem, dir);

            IEnumerable<string> paths = fileSource.GetFilePaths();

            Assert.That(paths, Is.EquivalentTo(new [] { filePath1 }));
        }
    }
}

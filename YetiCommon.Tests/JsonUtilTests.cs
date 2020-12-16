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

﻿
using NUnit.Framework;
using System.IO.Abstractions.TestingHelpers;

namespace YetiCommon.Tests
{
    [TestFixture]
    class JsonUtilTests
    {
        MockFileSystem filesystem;
        JsonUtil jsonUtil;

        [SetUp]
        public void SetUp()
        {
            filesystem = new MockFileSystem();
            filesystem.AddFile("/foo/bar", new MockFileData(@"{""Val1"":15,""Val2"":""Test""}"));
            filesystem.AddFile("/foo/invalid", new MockFileData(@"{""Val5""=""test""}"));

            jsonUtil = new JsonUtil(filesystem);
        }

        [Test]
        public void TestSerialize()
        {
            var simple = new Simple
            {
                Val1 = 15,
                Val2 = "Test",
            };

            var result = jsonUtil.Serialize(simple);
            Assert.That(result, Is.EqualTo(@"{""Val1"":15,""Val2"":""Test""}"));
        }

        [Test]
        public void TestDeserializeThrowsWhenEmptyString()
        {
            Assert.Throws<SerializationException>(() => jsonUtil.Deserialize<Simple>(""));
        }

        [Test]
        public void TestDeserializeThrowsWhenInvalidJson()
        {
            Assert.Throws<SerializationException>(
                () => jsonUtil.Deserialize<Simple>(@"{""Val5""=""test""}"));
        }

        [Test]
        public void TestDeserialize()
        {
            var result = jsonUtil.Deserialize<Simple>(@"{""Val1"":22,""Val2"":""TestTest""}");
            Assert.That(result.Val1, Is.EqualTo(22));
            Assert.That(result.Val2, Is.EqualTo("TestTest"));
        }

        [Test]
        public void LoadOrNull()
        {
            var result = jsonUtil.LoadOrNull<Simple>("/foo/bar");
            Assert.That(result.Val1, Is.EqualTo(15));
            Assert.That(result.Val2, Is.EqualTo("Test"));
        }

        [Test]
        public void LoadOrNull_FileNotFound()
        {
            var result = jsonUtil.LoadOrNull<Simple>("/foo/missing");
            Assert.That(result, Is.Null);
        }

        [Test]
        public void LoadOrNull_DirectoryNotFound()
        {
            var result = jsonUtil.LoadOrNull<Simple>("/does/not/exist");
            Assert.That(result, Is.Null);
        }

        [Test]
        public void LoadOrNull_Invalid()
        {
            var result = jsonUtil.LoadOrNull<Simple>("/foo/invalid");
            Assert.That(result, Is.Null);
        }

        [Test]
        public void LoadOrDefault()
        {
            var result = jsonUtil.LoadOrDefault<Simple>("/foo/bar");
            Assert.That(result.Val1, Is.EqualTo(15));
            Assert.That(result.Val2, Is.EqualTo("Test"));
        }

        [Test]
        public void LoadOrDefault_Invalid()
        {
            var result = jsonUtil.LoadOrDefault<Simple>("/foo/invalid");
            Assert.That(result.Val1, Is.EqualTo(0));
            Assert.That(result.Val2, Is.EqualTo(null));
        }

        private class Simple
        {
            public int Val1 { get; set; }

            public string Val2 { get; set; }
        }
    }
}

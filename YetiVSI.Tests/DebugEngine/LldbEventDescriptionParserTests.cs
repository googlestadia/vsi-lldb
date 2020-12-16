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
using YetiVSI.DebugEngine;

namespace YetiVSI.Test.DebugEngine
{
    /// <summary>
    /// Tests events' description field parsing. The parser should be able to get all the
    /// information about remote file download process. If received data is malformed no exception
    /// is thrown.
    /// </summary>
    /// <remarks>
    /// Event description sent by lldb looks as follows:
    /// <example>0000019CF1ADB980 Event: broadcaster = 0000019CE845EEE8 (lldb.stadia.broadcaster),
    /// type = 0x00000020 (file-update), data = {{\r\n  "file" :
    /// "/usr/local/cloudcast/lib/libggp.so",\r\n  "method" : 1,\r\n  "offset" : 10200,\r\n \"size\"
    /// : 1045\r\n}}</example> where:
    /// - `0000019CF1ADB980` pointer to the event
    /// - `0000019CE845EEE8` pointer to the broadcaster
    /// - `lldb.stadia.broadcaster` broadcaster name
    /// - `0x00000020` flag Process::eBroadcastBitStructuredData
    /// - `file-update` event name
    /// - `data` structured data object sent in the event
    ///     -- `file` path to the file being processed;
    ///     -- `size` file size
    ///     -- `offset` currently processed size
    ///     -- `method` either open/read or close enum value (zero-based).
    /// </remarks>

    [TestFixture]
    class LldbEventDescriptionParserTests
    {
        readonly LldbEventDescriptionParser _descriptionParser = new LldbEventDescriptionParser();

        [Test]
        public void ParseCorrectEventData([Values] FileProcessingState method, [Random(1)] int size)
        {
            var description =
                "this part will be ignored" +
                $", type = 0x00000020 (file-update), data = {{{{\r\n  \"file\" : \"/usr/local\",\r\n  \"method\" : {(int)method},\r\n  \"size\" : {size}\r\n}}}}";
            var parsed = _descriptionParser.Parse<FileProcessingUpdate>(description);

            Assert.That(parsed, Is.Not.Null);
            Assert.That(parsed.File, Is.EqualTo("/usr/local"));
            Assert.That(parsed.Method, Is.EqualTo(method));
            Assert.That(parsed.Size, Is.EqualTo(size));
        }

        [Test]
        public void ParseEventDataWithEmptyPayload()
        {
            var description =
                "this part will be ignored" +
                ", type = 0x00000020 (file-update), data = {{\n  \"file1\" : \"/usr/local/cloudcast/lib/libggp.so\"}}";
            var parsed = _descriptionParser.Parse<FileProcessingUpdate>(description);

            Assert.That(parsed, Is.Not.Null);
            Assert.That(parsed.File, Is.Null);
            Assert.That(parsed.Method, Is.EqualTo(FileProcessingState.Read));
            Assert.That(parsed.Size, Is.EqualTo(0));
        }

        [Test]
        public void ParseEventDataWithInvalidPayload()
        {
            var description =
                "0000019CF1ADB980 Event: broadcaster = 0000019CE845EEE8 (lldb.stadia.broadcaster), type = 0x00000020 (file-update), data = {{}}";
            var parsed = _descriptionParser.Parse<FileProcessingUpdate>(description);

            Assert.That(parsed, Is.Not.Null);
            Assert.That(parsed.File, Is.Null);
            Assert.That(parsed.Method, Is.EqualTo(FileProcessingState.Read));
            Assert.That(parsed.Size, Is.EqualTo(0));
        }

        [Test]
        public void ParseUnknownEventData([Values("0x00000040 (file-update)",
                                                  "0x00000020 (attaching)")] string type)
        {
            var description = $"something random, type = {type}, data = {{{{}}}}";
            var parsed = _descriptionParser.Parse<FileProcessingUpdate>(description);

            Assert.That(parsed, Is.Null);
        }

        [Test]
        public void ParseMalformedEventData()
        {
            var description =
                "0000019CF1ADB980 Event: broadcaster = 0000019CE845EEE8 (lldb.stadia.broadcaster), type = 0x00000020 (file-update), data = {\n  \"file1\" : \"/usr/local/cloudcast/lib/libggp.so\"}";
            var parsed = _descriptionParser.Parse<FileProcessingUpdate>(description);

            Assert.That(parsed, Is.Null);
        }
    }
}

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

﻿using Google.VisualStudioFake.Internal;
using NUnit.Framework;
using System.IO;
using TestsCommon.TestSupport;
using YetiCommon;

namespace Google.Tests.VisualStudioFake.Internal
{
    [TestFixture]
    [Explicit]
    class ProjectAdapterTests
    {
        // TODO Investigate solutions for defining this constant relative to the
        // source root on a developer machine.
        static readonly string _largeTestSamplesRoot = Path.Combine(
            YetiConstants.RootDir, @"..\..\LargeTestSamples");

        NLogSpy _nLogSpy;

        // Object under test.
        ProjectAdapter _projectAdapter;

        [SetUp]
        public void SetUp()
        {
            _nLogSpy = NLogSpy.CreateUnique(nameof(ProjectAdapterTests));
            _nLogSpy.Attach();

            _projectAdapter = new ProjectAdapter(_nLogSpy.GetLogger(), _largeTestSamplesRoot);
        }

        [Test]
        public void BuildHelloGgp()
        {
            _projectAdapter.Load("hello_ggp");
            Assert.DoesNotThrow(() => _projectAdapter.Build());
        }
    }
}
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

﻿using Google.VisualStudioFake.API;
using Microsoft.VisualStudio.Threading;
using NSubstitute;
using NUnit.Framework;

namespace Google.Tests.VisualStudioFake
{
    [TestFixture]
    public class VSFakeCompRootTests
    {
        // Object under test.
        VSFakeCompRoot compRoot;

        [SetUp]
        public void SetUp()
        {
            var config = new VSFakeCompRoot.Config {  SamplesRoot = @""  };
            var dgtFactoty = Substitute.For<IDebugQueryTargetFactory>();
            var taskContext = new JoinableTaskContext();
            var logger = Substitute.For<NLog.Logger>();
            compRoot = new VSFakeCompRoot(config, dgtFactoty, taskContext, logger);
        }

        [Test]
        public void CreateTest()
        {
            Assert.IsNotNull(compRoot.Create(() => null));
        }
    }
}

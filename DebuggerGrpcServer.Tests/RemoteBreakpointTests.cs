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
using NSubstitute;
using LldbApi;

namespace DebuggerGrpcServer.Tests
{
    [TestFixture]
    [Timeout(5000)]
    class RemoteBreakpointTests
    {
        SbBreakpoint mockBreakpoint;
        RemoteBreakpoint remoteBreakpoint;
        SbTarget mockTarget;
        RemoteTarget remoteTarget;
        SbFunction mockFunction;

        const string TEST_FUNCTION_NAME = "testFunctionName";
        const string TEST_FILE_NAME = "testFileName";
        const string TEST_DIR = "C:/";

        [SetUp]
        public void SetUp()
        {
            var breakpointFactory = new RemoteBreakpointFactory();
            mockBreakpoint = Substitute.For<SbBreakpoint>();
            remoteBreakpoint = breakpointFactory.Create(mockBreakpoint);
            mockTarget = Substitute.For<SbTarget>();
            remoteTarget = new RemoteTargetFactory(breakpointFactory).Create(mockTarget);
            mockFunction = Substitute.For<SbFunction>();
        }
    }
}
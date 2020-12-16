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

using NUnit.Framework;
using System.Linq;
using YetiVSI.DebugEngine.NatvisEngine;

namespace YetiVSI.Test.DebugEngine.NatvisEngine
{
    [TestFixture]
    public class NatvisDiagnosticLoggerTests
    {
        [Test]
        public void IsLogExhaustiveTest()
        {
            var levels = System.Enum.GetValues(typeof(NatvisLoggingLevel))
                .Cast<NatvisLoggingLevel>();
            var logFactory = new NLog.LogFactory();
            var logger = new NatvisDiagnosticLogger(logFactory.CreateNullLogger(),
                levels.Max());

            // Make sure that all logging level switch is exhaustive.
            // <c>logger.Log(...)</c> will throw an exception if the passed in level is not handled.
            foreach (var level in levels)
            {
                logger.Log(level, "test message");
            }
        }
    }
}

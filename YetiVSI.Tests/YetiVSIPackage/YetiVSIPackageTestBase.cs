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

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NUnit.Framework;
using System;
using System.ComponentModel.Design;
using YetiCommon;

namespace YetiVSI.Test.YetiVSIPackage
{
    // Base class that can be extended by test fixtures to test YetiVSIPackage integration tests.
    class YetiVSIPackageTestBase
    {
        private YetiVSI.YetiVSIPackage package;

        [SetUp]
        public virtual void SetUp()
        {
            package = new YetiVSI.YetiVSIPackage();
            package.InitializeForTest(new JoinableTaskContext());
        }

        [TearDown]
        public void TearDown()
        {
            package.UninitializeForTest();
        }

        /// <summary>
        /// Helper function to return a service from the package under test.
        /// </summary>
        /// <param name="type">The type of service to return.</param>
        /// <returns>A service of the requested type.</returns>
        protected object GetService(Type type)
        {
            return ((IServiceProvider)package).GetService(type);
        }

        /// <summary>
        /// Returns the Yeti command set handler for a given command id.
        /// </summary>
        /// <param name="cmdID">The command id of the handler to retrieve.</param>
        /// <returns>A MenuCommand instance if one exists, null otherwise.</returns>
        protected MenuCommand FindMenuCommand(int cmdID)
        {
            var menuCommandService =
                GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            Assert.That(menuCommandService, Is.Not.Null);
            return menuCommandService.FindCommand(
                new CommandID(YetiConstants.CommandSetGuid, cmdID));
        }
    }
}

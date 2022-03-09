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
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NSubstitute;
using NUnit.Framework;
using System;
using System.ComponentModel.Design;
using TestsCommon.TestSupport;
using YetiCommon;
using YetiVSI.LLDBShell;

namespace YetiVSI.Test.LLDBShell
{
    [TestFixture]
    class LLDBShellCommandTargetTests
    {
        private OleMenuCommandService menuCommandService;

        private IServiceProvider serviceProviderMock;

        private IVsCommandWindow commandWindowMock;

        private OptionPageGrid yetiOptions;

        private ILLDBShell shellMock;

        private LogSpy logSpy;

        // Captures all the text output to the |commandWindowMock|.
        private string commandWindowText;

        [SetUp]
        public void SetUp()
        {
            logSpy = new LogSpy();
            logSpy.Attach();

#pragma warning disable VSSDK005 // Avoid instantiating JoinableTaskContext
            var taskContext = new JoinableTaskContext();
#pragma warning restore VSSDK005 // Avoid instantiating JoinableTaskContext

            shellMock = Substitute.For<ILLDBShell>();

            commandWindowText = "";

            commandWindowMock = Substitute.For<IVsCommandWindow>();
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
            commandWindowMock.Print(Arg.Do<string>(x => commandWindowText += x));
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread

            serviceProviderMock = Substitute.For<IServiceProvider>();

            yetiOptions = Substitute.For<OptionPageGrid>();
            var yetiService = new YetiVSIService(yetiOptions);

#pragma warning disable VSSDK006 // Check services exist
            serviceProviderMock.GetService(typeof(YetiVSIService)).Returns(yetiService);
            serviceProviderMock.GetService(typeof(SLLDBShell)).Returns(shellMock);
            serviceProviderMock.GetService(typeof(SVsCommandWindow)).Returns(commandWindowMock);

            menuCommandService = new OleMenuCommandService(serviceProviderMock);
            serviceProviderMock.GetService(typeof(IMenuCommandService)).Returns(menuCommandService);
#pragma warning restore VSSDK006 // Check services exist

            LLDBShellCommandTarget.Register(taskContext, serviceProviderMock);
        }

        /// <summary>
        /// Invokes the LLDB Shell command.
        /// </summary>
        /// <param name="command">The LLDB shell command. Example: "help".</param>
        private void Invoke(object command)
        {
            var menuCommand = menuCommandService.FindCommand(
                new CommandID(YetiConstants.CommandSetGuid, PkgCmdID.cmdidLLDBShellExec));
            Assert.That(menuCommand, Is.Not.Null);
            menuCommand.Invoke(command);
        }

        [TearDown]
        public void Cleanup()
        {
            logSpy.Detach();
        }

        [Test]
        public void ExecWhenCommandIsntAString()
        {
            Invoke(12345);

            Assert.That(logSpy.GetOutput(), Does.Contain("ERROR"));
        }

        [Test]
        public void ExecWhenCommandIsEmptyString()
        {
            Invoke("");

            Assert.That(logSpy.GetOutput(), Does.Contain("ERROR"));
        }

        [Test]
        public void ExecWhenLLDBShellDoesntExist()
        {
#pragma warning disable VSSDK006 // Check services exist
            serviceProviderMock.GetService(typeof(SLLDBShell)).Returns(null);
#pragma warning restore VSSDK006 // Check services exist

            Invoke("help");

            Assert.That(logSpy.GetOutput(), Does.Contain("ERROR"));
        }

        [Test]
        public void ExecWhenLLDBShellThrowsException()
        {
            const string errorMessage = "Somthing Bad Happened";
            shellMock.When(x => x.ExecuteCommand("help"))
                .Do(x => { throw new Exception(errorMessage); });

            Invoke("help");

            Assert.That(logSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(logSpy.GetOutput(), Does.Contain(errorMessage));
        }

        [Test]
        public void ExecSuccessfully()
        {
            Invoke("help");

            Assert.That(commandWindowText, Does.Not.Contain("ERROR"));
            Assert.That(logSpy.GetOutput(), Does.Not.Contain("ERROR"));
        }
    }
}

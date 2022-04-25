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

using DebuggerApi;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NSubstitute;
using NUnit.Framework;
using TestsCommon.TestSupport;
using YetiVSITestsCommon;
using Task = System.Threading.Tasks.Task;

namespace YetiVSI.Test.LLDBShell
{
    [TestFixture]
    class LLDBShellTests
    {
        FakeMainThreadContext mainThreadContext;

        YetiVSI.LLDBShell.ILLDBShell shell;
        SbDebugger debuggerMock;
        SbCommandInterpreter commandInterpreterMock;
        SbCommandReturnObject returnObjectMock;
        IVsCommandWindow commandWindowMock;

        // Captures all the text output to the |commandWindowMock|.
        string commandWindowText;

        const string DUMMY_COMMAND = "dummy command";
        const string COMMAND_DESCRIPTION = "$omeRandomCmdDescription";

        LogSpy logSpy;
        CommandWindowWriter commandWindowWriter;

        [SetUp]
        public void SetUp()
        {
            mainThreadContext = new FakeMainThreadContext();

            logSpy = new LogSpy();
            logSpy.Attach();

            returnObjectMock = Substitute.For<SbCommandReturnObject>();
            returnObjectMock.GetDescription().Returns(COMMAND_DESCRIPTION);

            commandInterpreterMock = Substitute.For<SbCommandInterpreter>();
            commandInterpreterMock.WhenForAnyArgs(x => x.HandleCommand(DUMMY_COMMAND,
                out returnObjectMock)).Do(x => x[1] = returnObjectMock);

            debuggerMock = Substitute.For<SbDebugger>();
            debuggerMock.GetCommandInterpreter().Returns(commandInterpreterMock);

            commandWindowText = "";

            commandWindowMock = Substitute.For<IVsCommandWindow>();
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
            commandWindowMock.Print(Arg.Do<string>(x => commandWindowText += x));
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread

            commandWindowWriter = new CommandWindowWriter(commandWindowMock);

            shell = new YetiVSI.LLDBShell.LLDBShell(commandWindowWriter);
            shell.AddDebugger(debuggerMock);
        }

        [TearDown]
        public void Cleanup()
        {
            logSpy.Detach();
            mainThreadContext.Dispose();
        }

        [Test]
        public async Task ExecuteCommandWithNoDebuggerAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            ClearDebuggers();

            shell.ExecuteCommand(DUMMY_COMMAND);

            Assert.That(commandWindowText, Does.Contain("ERROR"));
            Assert.That(logSpy.GetOutput(), Does.Contain("ERROR"));
            commandInterpreterMock.DidNotReceiveWithAnyArgs().HandleCommand(Arg.Any<string>(),
                out _);
        }

        [Test]
        public async Task ExecuteCommandWithMultipleDebuggersAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var secondDebuggerMock = Substitute.For<SbDebugger>();

            ClearDebuggers();
            shell.AddDebugger(debuggerMock);
            shell.AddDebugger(secondDebuggerMock);

            shell.ExecuteCommand(DUMMY_COMMAND);


            Assert.That(commandWindowText, Does.Contain("ERROR"));
            Assert.That(logSpy.GetOutput(), Does.Contain("ERROR"));

            debuggerMock.DidNotReceive().GetCommandInterpreter();
            secondDebuggerMock.DidNotReceive().GetCommandInterpreter();
            commandInterpreterMock.DidNotReceiveWithAnyArgs().HandleCommand(Arg.Any<string>(),
                out _);

            Assert.That(logSpy.GetOutput(), Does.Contain(DUMMY_COMMAND));
        }

        [Test]
        public async Task ExecuteCommandWithNoCommandInterpreterAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            debuggerMock.GetCommandInterpreter().Returns((SbCommandInterpreter)null);

            shell.ExecuteCommand(DUMMY_COMMAND);

            Assert.That(commandWindowText, Does.Contain("ERROR"));
            Assert.That(logSpy.GetOutput(), Does.Contain("ERROR"));
            commandInterpreterMock.DidNotReceiveWithAnyArgs().HandleCommand(Arg.Any<string>(),
                out _);
        }

        [Test]
        public async Task ExecuteCommandWithNullResultAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            commandInterpreterMock.WhenForAnyArgs(x => x.HandleCommand(DUMMY_COMMAND,
                out returnObjectMock)).Do(x => x[1] = null);

            shell.ExecuteCommand(DUMMY_COMMAND);

            Assert.That(commandWindowText, Does.Contain("ERROR"));
            Assert.That(logSpy.GetOutput(), Does.Contain("ERROR"));
        }

        [Test]
        public async Task ExecuteCommandSuccessfullyAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            shell.ExecuteCommand(DUMMY_COMMAND);

            Assert.That(commandWindowText, Does.Contain(COMMAND_DESCRIPTION));
            Assert.That(logSpy.GetOutput(), Does.Contain(DUMMY_COMMAND));

            Assert.That(commandWindowText, Does.Not.Contain("ERROR"));
            Assert.That(logSpy.GetOutput(), Does.Not.Contain("ERROR"));
        }

        [Test]
        public async Task ExecuteCommandWithNoCommandWindowAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // CommandWindowWriter grabs the SVsCommandWindow once in the constructor. If we change
            // the service provide mock we need to create a new CommandwindowWriter to pick up the
            // change.
            commandWindowWriter = new CommandWindowWriter(null);
            shell = new YetiVSI.LLDBShell.LLDBShell(commandWindowWriter);
            shell.AddDebugger(debuggerMock);

            shell.ExecuteCommand(DUMMY_COMMAND);

            Assert.That(logSpy.GetOutput(), Does.Contain(COMMAND_DESCRIPTION));
            Assert.That(logSpy.GetOutput(), Does.Contain(DUMMY_COMMAND));
        }

        private void ClearDebuggers()
        {
            ((YetiVSI.LLDBShell.LLDBShell)shell).ClearAllDebuggers();
        }
    }
}

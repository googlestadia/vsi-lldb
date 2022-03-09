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
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NSubstitute;
using NUnit.Framework;
using System;
using TestsCommon.TestSupport;

namespace YetiVSI.Test.LLDBShell
{
    [TestFixture]
    class LLDBShellTests
    {
        private JoinableTaskContext taskContext;

        // Test target.
        private YetiVSI.LLDBShell.ILLDBShell shell;

        private SbDebugger debuggerMock;

        private SbCommandInterpreter commandInterpreterMock;

        private SbCommandReturnObject returnObjectMock;

        // Used to satisfy out argument requirements when verifying HandleCommand() was not called.
        private SbCommandReturnObject dummyReturnObject;

        private IVsCommandWindow commandWindowMock;

        // Captures all the text output to the |commandWindowMock|.
        private string commandWindowText;

        private const string DUMMY_COMMAND = "dummy command";

        private const string COMMAND_DESCRIPTION = "$omeRandomCmdDescription";

        private LogSpy logSpy;

        private CommandWindowWriter commandWindowWriter;

        [SetUp]
        public void SetUp()
        {
            logSpy = new LogSpy();
            logSpy.Attach();

#pragma warning disable VSSDK005 // Avoid instantiating JoinableTaskContext
            taskContext = new JoinableTaskContext();
#pragma warning restore VSSDK005 // Avoid instantiating JoinableTaskContext

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

            commandWindowWriter = new CommandWindowWriter(taskContext, commandWindowMock);

            shell = new YetiVSI.LLDBShell.LLDBShell(taskContext, commandWindowWriter);
            shell.AddDebugger(debuggerMock);
        }

        [TearDown]
        public void Cleanup()
        {
            logSpy.Detach();
        }

        [Test]
        public void ExecuteCommandWithNoDebugger()
        {
            ClearDebuggers();

            shell.ExecuteCommand(DUMMY_COMMAND);

            Assert.That(commandWindowText, Does.Contain("ERROR"));
            Assert.That(logSpy.GetOutput(), Does.Contain("ERROR"));
            commandInterpreterMock.DidNotReceiveWithAnyArgs().HandleCommand(Arg.Any<string>(),
                out dummyReturnObject);
        }

        [Test]
        public void ExecuteCommandWithMultipleDebuggers()
        {
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
                out dummyReturnObject);

            Assert.That(logSpy.GetOutput(), Does.Contain(DUMMY_COMMAND));
        }

        [Test]
        public void ExecuteCommandWithNoCommandInterpreter()
        {
            debuggerMock.GetCommandInterpreter().Returns((SbCommandInterpreter)null);

            shell.ExecuteCommand(DUMMY_COMMAND);

            Assert.That(commandWindowText, Does.Contain("ERROR"));
            Assert.That(logSpy.GetOutput(), Does.Contain("ERROR"));
            commandInterpreterMock.DidNotReceiveWithAnyArgs().HandleCommand(Arg.Any<string>(),
                out dummyReturnObject);
        }

        [Test]
        public void ExecuteCommandWithNullResult()
        {
            commandInterpreterMock.WhenForAnyArgs(x => x.HandleCommand(DUMMY_COMMAND,
                out returnObjectMock)).Do(x => x[1] = null);

            shell.ExecuteCommand(DUMMY_COMMAND);

            Assert.That(commandWindowText, Does.Contain("ERROR"));
            Assert.That(logSpy.GetOutput(), Does.Contain("ERROR"));
        }

        [Test]
        public void ExecuteCommandSuccessfully()
        {
            shell.ExecuteCommand(DUMMY_COMMAND);

            Assert.That(commandWindowText, Does.Contain(COMMAND_DESCRIPTION));
            Assert.That(logSpy.GetOutput(), Does.Contain(DUMMY_COMMAND));

            Assert.That(commandWindowText, Does.Not.Contain("ERROR"));
            Assert.That(logSpy.GetOutput(), Does.Not.Contain("ERROR"));
        }

        [Test]
        public void ExecuteCommandWithNoCommandWindow()
        {
            // CommandWindowWriter grabs the SVsCommandWindow once in the constructor. If we change
            // the service provide mock we need to create a new CommandwindowWriter to pick up the
            // change.
            commandWindowWriter = new CommandWindowWriter(taskContext, null);
            shell = new YetiVSI.LLDBShell.LLDBShell(taskContext, commandWindowWriter);
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

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

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NSubstitute;
using NUnit.Framework;
using TestsCommon.TestSupport;
using YetiCommon;
using YetiVSI.DebugEngine;
using YetiVSI.DebuggerOptions;
using YetiVSITestsCommon;
using Task = System.Threading.Tasks.Task;

namespace YetiVSI.Test.DebuggerOptions
{
    [TestFixture]
    class DebuggerOptionsCommandTests
    {
        FakeMainThreadContext mainThreadContext;
        OleMenuCommandService menuCommandService;
        IServiceProvider serviceProviderMock;
        IVsCommandWindow commandWindowMock;
        IDebugEngineCommands debugEngineCommandsMock;
        IGgpDebugEngine debugEngineMock;
        DebugEngineManager debugEngineManager;
        LogSpy logSpy;
        YetiVSIService yetiService;

        // Captures all the text output to the |commandWindowMock|.
        string commandWindowText;

        [SetUp]
        public async Task SetUpAsync()
        {
            mainThreadContext = new FakeMainThreadContext();

            logSpy = new LogSpy();
            logSpy.Attach();

            commandWindowText = "";

            commandWindowMock = Substitute.For<IVsCommandWindow>();
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
            commandWindowMock.Print(Arg.Do<string>(x => commandWindowText += x));
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread

            serviceProviderMock = Substitute.For<IServiceProvider>();

            yetiService = new YetiVSIService(null);

#pragma warning disable VSSDK006 // Check services exist
            serviceProviderMock.GetService(typeof(YetiVSIService)).Returns(yetiService);
            serviceProviderMock.GetService(typeof(SVsCommandWindow)).Returns(commandWindowMock);
#pragma warning restore VSSDK006 // Check services exist

            debugEngineCommandsMock = Substitute.For<IDebugEngineCommands>();

            // (internal): This needs to be a member variable since debugEngineManager tracks it
            //              by weak reference only!.
            debugEngineMock = Substitute.For<IGgpDebugEngine>();
            debugEngineMock.DebugEngineCommands.Returns(debugEngineCommandsMock);

            debugEngineManager = new DebugEngineManager();
            debugEngineManager.AddDebugEngine(debugEngineMock);

#pragma warning disable VSSDK006 // Check services exist
            serviceProviderMock.GetService(typeof(SDebugEngineManager))
                .Returns(debugEngineManager);

            menuCommandService = new OleMenuCommandService(serviceProviderMock);
            serviceProviderMock.GetService(typeof(IMenuCommandService))
                .Returns(menuCommandService);
#pragma warning restore VSSDK006 // Check services exist

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            DebuggerOptionsCommand.Register(serviceProviderMock);
        }

        /// <summary>
        /// Invokes the Stadia Debugger Options command.
        /// </summary>
        /// <param name="command">The Stadia Debugger Options command. Example: "list".</param>
        void Invoke(object command)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                MenuCommand menuCommand = menuCommandService.FindCommand(
                    new CommandID(YetiConstants.CommandSetGuid, PkgCmdID.cmdidDebuggerOptionsCommand));
                Assert.That(menuCommand, Is.Not.Null);
                menuCommand.Invoke(command);
            });
        }

        [TearDown]
        public void Cleanup()
        {
            logSpy.Detach();
            mainThreadContext.Dispose();
        }

        [Test]
        public void ExecutedCommandCapturedInLogs()
        {
            Invoke("list --help");

            Assert.That(logSpy.GetOutput(), Does.Contain("list --help"));
        }

        [Test]
        public void InvalidCommand()
        {
            Assert.Throws<UnrecognizedCommandParsingException>(
                () => Invoke("invalid_command --option -o arg"));

            Assert.That(commandWindowText, Does.Contain("Error"));
            Assert.That(logSpy.GetOutput(), Does.Contain("Unrecognized command"));
            Assert.That(logSpy.GetOutput(), Does.Contain("invalid_command --option -o arg"));
        }

        [Test]
        public void ExecWhenCommandIsntAString()
        {
            Invoke(12345);

            Assert.That(commandWindowText, Does.Contain("ERROR"));
            Assert.That(logSpy.GetOutput(), Does.Contain("ERROR"));
        }

        [Test]
        public void ExecWhenCommandIsEmptyString()
        {
            Invoke("");

            Assert.That(commandWindowText, Does.Contain("Usage:  [options] [command]"));
        }

        [Test]
        public void ExecWithHelpOption()
        {
            Invoke("--help");

            Assert.That(commandWindowText, Does.Contain("Usage:  [options] [command]"));
        }

        [Test]
        public void EnableCommand()
        {
            yetiService.DebuggerOptions[DebuggerOption.CLIENT_LOGGING] =
                DebuggerOptionState.DISABLED;

            Invoke("enable client_logging");

            Assert.That(commandWindowText, Does.Not.Contain("ERROR"));
            Assert.That(logSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.AreEqual(DebuggerOptionState.ENABLED,
                yetiService.DebuggerOptions[DebuggerOption.CLIENT_LOGGING]);
        }

        [Test]
        public void EnableWithNoArgs()
        {
            Invoke("enable");

            Assert.That(commandWindowText, Does.Contain("No options specified."));
            Assert.That(logSpy.GetOutput(), Does.Contain("No options specified."));

            Assert.That(commandWindowText, Does.Contain("Stadia.Debugger list"));
            Assert.That(logSpy.GetOutput(), Does.Contain("Stadia.Debugger list"));
        }

        [Test]
        public void EnableInvalidOption()
        {
            Invoke("enable test_option");

            Assert.That(commandWindowText, Does.Contain("ERROR"));
            Assert.That(logSpy.GetOutput(), Does.Contain("ERROR"));

            Assert.That(commandWindowText, Does.Contain("Stadia.Debugger list"));
            Assert.That(logSpy.GetOutput(), Does.Contain("Stadia.Debugger list"));
        }

        [Test]
        public void EnableCommandWithMultiplArgs()
        {
            yetiService.DebuggerOptions[DebuggerOption.CLIENT_LOGGING] =
                DebuggerOptionState.DISABLED;
            yetiService.DebuggerOptions[DebuggerOption.PARAMETER_LOGGING] =
                DebuggerOptionState.DISABLED;

            Invoke("enable client_logging parameter_logging");

            Assert.That(commandWindowText, Does.Not.Contain("ERROR"));
            Assert.That(logSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.AreEqual(DebuggerOptionState.ENABLED,
                yetiService.DebuggerOptions[DebuggerOption.CLIENT_LOGGING]);
            Assert.AreEqual(DebuggerOptionState.ENABLED,
                yetiService.DebuggerOptions[DebuggerOption.PARAMETER_LOGGING]);
        }

        [Test]
        public void DisableCommand()
        {
            yetiService.DebuggerOptions[DebuggerOption.CLIENT_LOGGING] =
                DebuggerOptionState.ENABLED;

            Invoke("disable client_logging");

            Assert.That(commandWindowText, Does.Not.Contain("ERROR"));
            Assert.That(logSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.AreEqual(DebuggerOptionState.DISABLED,
                yetiService.DebuggerOptions[DebuggerOption.CLIENT_LOGGING]);
        }

        [Test]
        public void DisableWithNoArgs()
        {
            Invoke("disable");

            Assert.That(commandWindowText, Does.Contain("No options specified."));
            Assert.That(logSpy.GetOutput(), Does.Contain("No options specified."));

            Assert.That(commandWindowText, Does.Contain("Stadia.Debugger list"));
            Assert.That(logSpy.GetOutput(), Does.Contain("Stadia.Debugger list"));
        }

        [Test]
        public void DisableInvalidOption()
        {
            Invoke("disable test_option");

            Assert.That(commandWindowText, Does.Contain("ERROR"));
            Assert.That(logSpy.GetOutput(), Does.Contain("ERROR"));

            Assert.That(commandWindowText, Does.Contain("Stadia.Debugger list"));
            Assert.That(logSpy.GetOutput(), Does.Contain("Stadia.Debugger list"));
        }

        [Test]
        public void DisableCommandWithMultipleArgs()
        {
            yetiService.DebuggerOptions[DebuggerOption.CLIENT_LOGGING] =
                DebuggerOptionState.ENABLED;
            yetiService.DebuggerOptions[DebuggerOption.PARAMETER_LOGGING] =
                DebuggerOptionState.ENABLED;

            Invoke("disable client_logging parameter_logging");

            Assert.That(commandWindowText, Does.Not.Contain("ERROR"));
            Assert.That(logSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.AreEqual(DebuggerOptionState.DISABLED,
                yetiService.DebuggerOptions[DebuggerOption.CLIENT_LOGGING]);
            Assert.AreEqual(DebuggerOptionState.DISABLED,
                yetiService.DebuggerOptions[DebuggerOption.PARAMETER_LOGGING]);
        }

        [Test]
        public void ListCommand()
        {
            // Enable a command to test that changed values are reported correctly.
            yetiService.DebuggerOptions[DebuggerOption.SKIP_WAIT_LAUNCH] =
                DebuggerOptionState.ENABLED;

            Invoke("list");

            Assert.Multiple(() =>
            {
                // Check some options with default values
                Assert.That(commandWindowText, Does.Contain("client_logging: disabled"));
                Assert.That(commandWindowText, Does.Contain("server_logging: disabled"));

                // Check option that had value changed
                Assert.That(commandWindowText, Does.Contain("skip_wait_launch: enabled"));

                // Check an option with a default value of "enabled"
                Assert.That(commandWindowText, Does.Contain("exception_logging: enabled"));
            });
        }

        [Test]
        public void ListInvalidArgCount()
        {
            Assert.Throws<UnrecognizedCommandParsingException>(() => Invoke("list test"));

            Assert.That(commandWindowText, Does.Contain("Error"));
            Assert.That(logSpy.GetOutput(), Does.Contain("Unrecognized command"));
        }

        [Test]
        public void ResetCommand()
        {
            Invoke("reset");

            Assert.That(commandWindowText, Does.Not.Contain("ERROR"));
            Assert.That(logSpy.GetOutput(), Does.Not.Contain("ERROR"));
        }

        [Test]
        public void ResetInvalidArgCount()
        {
            Assert.Throws<UnrecognizedCommandParsingException>(() => Invoke("reset test"));

            Assert.That(commandWindowText, Does.Contain("Error"));
            Assert.That(logSpy.GetOutput(), Does.Contain("Unrecognized command"));
        }

        [Test]
        public void RunLogNatvisStats()
        {
            int verbosityLevel = -1;
            debugEngineCommandsMock.When(
                x => x.LogNatvisStats(Arg.Any<TextWriter>(), Arg.Any<int>())).Do(x =>
                {
                    ((TextWriter)x[0]).WriteLine("Logged Natvis Stats");
                    verbosityLevel = (int)x[1];
                });

            Invoke("run logNatvisStats");

            Assert.That(verbosityLevel, Is.EqualTo(0));
            Assert.That(commandWindowText, Does.Contain("Logged Natvis Stats"));
        }

        [Test]
        public void RunLogNatvisStatsVerbose()
        {
            int verbosityLevel = -1;
            debugEngineCommandsMock.When(
                x => x.LogNatvisStats(Arg.Any<TextWriter>(), Arg.Any<int>())).Do(x =>
                {
                    ((TextWriter)x[0]).WriteLine("Logged Natvis Stats");
                    verbosityLevel = (int)x[1];
                });

            Invoke("run logNatvisStats -v");

            Assert.That(verbosityLevel, Is.EqualTo(1));
            Assert.That(commandWindowText, Does.Contain("Logged Natvis Stats"));
        }

        [Test]
        public void RunLogNatvisStatsWithoutDebugEngine()
        {
            // This will cause the debug engine to be purged from debugEngineManager when
            // the Natvis command calls GetDebugEngines().
            debugEngineMock = null;
            GC.Collect();

            Invoke("run logNatvisStats");

            Assert.That(commandWindowText, Does.Contain("Failed to find an active debug session"));
        }

        [Test]
        public void Enumerable()
        {
            // Enable a command to test that changed values are reported correctly.
            yetiService.DebuggerOptions[DebuggerOption.SKIP_WAIT_LAUNCH] =
                DebuggerOptionState.ENABLED;

            var expected = new Dictionary<DebuggerOption, DebuggerOptionState>(
                (IDictionary<DebuggerOption, DebuggerOptionState>)
                    YetiVSI.DebuggerOptions.DebuggerOptions.Defaults);
            expected[DebuggerOption.SKIP_WAIT_LAUNCH] = DebuggerOptionState.ENABLED;
            CollectionAssert.AreEquivalent(yetiService.DebuggerOptions, expected);
        }
    }
}

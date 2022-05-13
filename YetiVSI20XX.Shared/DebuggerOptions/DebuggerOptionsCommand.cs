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
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Collections.Concurrent;
using System.Text;
using YetiCommon;
using Microsoft.VisualStudio.Threading;
using YetiVSI.Util;
using McMaster.Extensions.CommandLineUtils;
using System.IO;
using System.Diagnostics;
using YetiVSI.DebugEngine;
using Microsoft.VisualStudio.Shell.Interop;
using System.Collections;

namespace YetiVSI.DebuggerOptions
{
    /// <summary>
    /// Identifies the debugger options available via the "Stadia.Debugger enable|disable" commands.
    /// </summary>
    /// <remarks>
    /// The names of these enum values are recorded in DevTools metrics. Avoid changing names unless
    /// you are changing the semantic meaning of the option.
    /// </remarks>
    public enum DebuggerOption
    {
        // Generates a call sequence diagram from YetiVSI DebugEngine calls and writes it to
        // YetiVSI.CallSequenceDiagram log file.
        CALL_SEQUENCE_LOGGING,
        // Capture a subset of LLDB client logging categories (mainly just default). The logs will
        // be written out to the YetiVSI log file.
        // Note: Enabling this will impact performance of the debugger.
        CLIENT_LOGGING,
        // Capture traces while running the YetiVSI DebugEngine and write them to YetiVSI.Trace log
        // file. This is an experimental feature that may result in degraded performance and/or
        // bugs.
        DEBUGGER_TRACING,
        // Enables unhandled exception logging while running the YetiVSI DebugEngine and writes
        // them to YetiVSI log file.
        // Enabled by default.
        EXCEPTION_LOGGING,
        // Enables automatic recording of metrics about unhandled exceptions in the debugger.
        // Enabled by default.
        EXCEPTION_METRICS,
        // Capture Grpc logs to the YetiVSI log file.
        GRPC_LOGGING,
        // Enables experimental Natvis features.  Performance and stability may be degraded when
        // enabled.
        NATVIS_EXPERIMENTAL,
        // Captures parameter value logging while running the YetiVSI DebugEngine and write them to
        // YetiVSI log file.  This is an experimental feature that may result in degraded
        // performance.
        PARAMETER_LOGGING,
        // Capture a subset of LLDB server logging categories (mainly just default). The logs will
        // be written out to the YetiVSI log file.
        // Note: Enabling this will impact performance of the debugger.
        SERVER_LOGGING,
        // Skip waiting until the debugger is attached before starting the game.
        SKIP_WAIT_LAUNCH,
        // Enables metrics about step time to be collected.
        // Enabled by default.
        STEP_TIME_METRICS,
    }

    /// <summary>
    /// Possible values for debugger options.
    /// </summary>
    /// <remarks>
    /// These values are recorded in DevTools metrics as integers. Avoid changing values unless you
    /// are changing the semantic meaning of the value.
    /// </remarks>
    public enum DebuggerOptionState
    {
        DISABLED = 0,
        ENABLED = 1,
    }

    public class DebuggerOptions : IEnumerable<KeyValuePair<DebuggerOption, DebuggerOptionState>>
    {
        public static readonly IReadOnlyDictionary<DebuggerOption, DebuggerOptionState> Defaults =
            new Dictionary<DebuggerOption, DebuggerOptionState>()
            {
                {DebuggerOption.CALL_SEQUENCE_LOGGING, DebuggerOptionState.DISABLED},
                {DebuggerOption.CLIENT_LOGGING, DebuggerOptionState.DISABLED},
                {DebuggerOption.DEBUGGER_TRACING, DebuggerOptionState.DISABLED},
                {DebuggerOption.EXCEPTION_LOGGING, DebuggerOptionState.ENABLED},
                {DebuggerOption.EXCEPTION_METRICS, DebuggerOptionState.ENABLED},
                {DebuggerOption.GRPC_LOGGING, DebuggerOptionState.DISABLED},
                {DebuggerOption.NATVIS_EXPERIMENTAL, DebuggerOptionState.DISABLED},
                {DebuggerOption.PARAMETER_LOGGING, DebuggerOptionState.DISABLED},
                {DebuggerOption.SERVER_LOGGING, DebuggerOptionState.DISABLED},
                {DebuggerOption.SKIP_WAIT_LAUNCH, DebuggerOptionState.DISABLED},
                {DebuggerOption.STEP_TIME_METRICS, DebuggerOptionState.ENABLED},
            };

        public EventHandler<ValueChangedEventArgs> ValueChanged;

        public class ValueChangedEventArgs
        {
            public ValueChangedEventArgs(DebuggerOption option, DebuggerOptionState state)
            {
                Option = option;
                State = state;
            }

            public DebuggerOption Option { get; }

            public DebuggerOptionState State { get; }
        }

        IDictionary<DebuggerOption, DebuggerOptionState> options;

        public DebuggerOptions()
        {
            // Disable all options by default.
            Reset();
        }

        IEnumerator IEnumerable.GetEnumerator() => options.GetEnumerator();

        public IEnumerator<KeyValuePair<DebuggerOption, DebuggerOptionState>> GetEnumerator()
            => options.GetEnumerator();

        public KeyValuePair<DebuggerOption, DebuggerOptionState> Last() => options.Last();

        public void Reset()
        {
            // TODO: trigger ValueChanged when resetting values.
            options = new ConcurrentDictionary<DebuggerOption, DebuggerOptionState>(Defaults);
        }

        public DebuggerOptionState this[DebuggerOption option]
        {
            get
            {
                return options[option];
            }
            set
            {
                if (options[option] != value)
                {
                    options[option] = value;
                    ValueChanged?.Invoke(this, new ValueChangedEventArgs(option, value));
                }
            }
        }
    }

    // Hidden debugger options that can be accessed by a command in the Visual Studio command
    // window. These commands are not meant to be partner facing and will not have a 'help' command.
    // They are debug options that partners should only be using if we've specifically told them
    // about the options (either to help debug, or work around an issue, etc...) These
    // are temporary debugger options that are not persisted when Visual Studio is restarted. There
    // will not be any partner facing documentation as they aren't meant to be used to self
    // diagnose issues.
    public class DebuggerOptionsCommand
    {
        /// <summary>
        /// TextWriter adapter used by the CommandLineApplication class.
        /// </summary>
        class CommandTextWriter : TextWriter
        {
            readonly CommandWindowWriter cmdWindowWriter;

            public CommandTextWriter(CommandWindowWriter cmdWindowWriter)
            {
                this.cmdWindowWriter = cmdWindowWriter;
            }

            public override Encoding Encoding => Encoding.Default;

            public override void Write(string value)
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                cmdWindowWriter.Print(value);
            }

            public override void WriteLine(string value)
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                cmdWindowWriter.PrintLine(value);
            }

            public override void Write(char c)
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                cmdWindowWriter.Print($"{c}");
            }
        }

        readonly IDebugEngineManager debugEngineManager;
        readonly YetiVSIService vsiService;
        readonly CommandWindowWriter commandWindowWriter;

        static public DebuggerOptionsCommand Register(IServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var menuCommand = new DebuggerOptionsCommand(
                (YetiVSIService)serviceProvider.GetService(typeof(YetiVSIService)),
                (IDebugEngineManager)serviceProvider.GetService(typeof(SDebugEngineManager)),
                (IVsCommandWindow)serviceProvider.GetService(typeof(SVsCommandWindow)),
                serviceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService);

            return menuCommand;
        }

        DebuggerOptionsCommand(
            YetiVSIService vsiService,
            IDebugEngineManager debugEngineManager,
            IVsCommandWindow commandWindow,
            IMenuCommandService menuCommandService)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            this.debugEngineManager = debugEngineManager;
            this.vsiService = vsiService;

            commandWindowWriter = new CommandWindowWriter(commandWindow);
            var menuCommand = new OleMenuCommand(HandleCommand,
                new CommandID(YetiConstants.CommandSetGuid, PkgCmdID.cmdidDebuggerOptionsCommand));
            // Accept any parameter value.
            menuCommand.ParametersDescription = "$";
            menuCommandService?.AddCommand(menuCommand);
        }

        void HandleCommand(object sender, EventArgs eventArgs)
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                var cmdEventArgs = eventArgs as OleMenuCmdEventArgs;
                if (cmdEventArgs == null || !(cmdEventArgs.InValue is string))
                {
                    commandWindowWriter.PrintErrorMsg("ERROR: Unable to parse command.");
                    return;
                }

                var commandText = (string)cmdEventArgs.InValue;
                var strArgs =
                    commandText.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);

                Trace.WriteLine($"Executing 'Stadia.Debugger {commandText}'");

                var result = BuildCommandParser().Execute(strArgs);
                Trace.WriteLine($"'Stadia.Debugger {commandText}' returned {result}.");
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                commandWindowWriter.PrintErrorMsg(
                    $"Error - See logs for more info.");
                // The exception message will be printed to the command window by Visual Studio.

                Trace.WriteLine(
                    $"ERROR: Caught unexpected exception executing 'Stadia.Debugger' command: " +
                    $"{ex.Demystify()}");
                throw;
            }
        }

        CommandLineApplication BuildCommandParser()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var stadiaCmd = new CommandLineApplication();
            stadiaCmd.MakeSuggestionsInErrorMessage = true;

            var cmdTextWriter = new CommandTextWriter(commandWindowWriter);
            stadiaCmd.Error = cmdTextWriter;
            stadiaCmd.Out = cmdTextWriter;

            // Remove the default "-?" option because this is handled by Visual Studio in relation
            // to the higher level command, e.g. Staida.Debugger.
            //
            // TODO: Figure out why inherited:true is not getting text output for
            // subcommands.
            stadiaCmd.HelpOption("-h|--help", inherited:true);
            stadiaCmd.ExtendedHelpText = "Remarks: These commands/features are experimental in " +
                "nature and not officially supported.";

            stadiaCmd.Command("enable", enableCmd =>
            {
                enableCmd.Description = "Enables an experimental feature.";
                var debugOptions = enableCmd.Argument("feature", "Name of feature",
                    multipleValues: true);
                enableCmd.OnExecute(() =>
                {
                    if (debugOptions.Values.Count == 0)
                    {
                        PrintListFeatureHint();
                        return 0;
                    }
                    SetFeatureConfig(DebuggerOptionState.ENABLED, debugOptions.Values);
                    return 0;
                });
            });

            stadiaCmd.Command("disable", enableCmd =>
            {
                enableCmd.Description = "Disables an experimental feature.";
                var debugOptions = enableCmd.Argument("feature", "Name of feature",
                    multipleValues:true);
                enableCmd.OnExecute(() =>
                {
                    if (debugOptions.Values.Count == 0)
                    {
                        PrintListFeatureHint();
                        return 0;
                    }
                    SetFeatureConfig(DebuggerOptionState.DISABLED, debugOptions.Values);
                    return 0;
                });
            });

            stadiaCmd.Command("list", listCmd =>
            {
                listCmd.Description = "Lists the current experimental feature settings.";
                listCmd.OnExecute(() =>
                {
                    PrintFeatureConfig();
                    return 0;
                });
            });

            stadiaCmd.Command("reset", resetCmd =>
            {
                resetCmd.Description = "Resets experimental features to the default setting.";
                resetCmd.OnExecute(() =>
                {
                    ResetFeatureConfig();
                    return 0;
                });
            });

            stadiaCmd.Command("run", runCmd =>
            {
                runCmd.Description = "Executes a given command.";

                runCmd.Command("logNatvisStats", logNatvisStatsCmd =>
                {
                    logNatvisStatsCmd.Description =
                        "Prints Natvis stats to the Output window and log files.";

                    var verboseOption = logNatvisStatsCmd.VerboseOption();

                    logNatvisStatsCmd.OnExecute(() =>
                    {
                        LogNatvisStats(verboseOption.Values.Count);
                        return 0;
                    });
                });
            });

            stadiaCmd.OnExecute(() =>
            {
                stadiaCmd.ShowHelp();
                return 1;
            });

            return stadiaCmd;
        }

        void SetFeatureConfig(DebuggerOptionState optionState, IEnumerable<string> arguments)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var stringBuilder = new StringBuilder();
            foreach (var argument in arguments)
            {
                DebuggerOption option;
                try
                {
                    option = (DebuggerOption)Enum.Parse(typeof(DebuggerOption), argument, true);
                }
                catch (Exception ex) when (
                  ex is ArgumentException
                  || ex is OverflowException
                )
                {
                    commandWindowWriter.PrintErrorMsg($"ERROR: Invalid argument '{argument}'. " +
                        "Argument must be a valid option name, use 'Stadia.Debugger list' to " +
                        "view valid option names.");
                    continue;
                }
                vsiService.DebuggerOptions[option] = optionState;
                stringBuilder.AppendLine(
                    $"{option.ToString().ToLower()}: {optionState.ToString().ToLower()}");
            }
            commandWindowWriter.Print(stringBuilder.ToString());
        }

        void PrintListFeatureHint()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            commandWindowWriter.PrintErrorMsg(
                "No options specified.  Use 'Stadia.Debugger list' to view valid option names.");
        }

        void PrintFeatureConfig()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var output = new StringBuilder();
            foreach (var option in vsiService.DebuggerOptions)
            {
                output.Append(string.Format("{0}: {1}", option.Key.ToString().ToLower(),
                    option.Value.ToString().ToLower()));
                if (!vsiService.DebuggerOptions.Last().Equals(option))
                {
                    output.AppendLine();
                }
            }
            commandWindowWriter.Print(output.ToString());
        }

        void ResetFeatureConfig()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            vsiService.DebuggerOptions.Reset();
            commandWindowWriter.PrintLine("All options reset to default values.");
        }

        void LogNatvisStats(int verbosityLevel)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var debugEngine = TryGetDebugEngine();
            if (debugEngine == null)
            {
                return;
            }

            using (var writer = new StringWriter())
            {
                debugEngine.DebugEngineCommands.LogNatvisStats(writer, verbosityLevel);
                writer.Flush();
                commandWindowWriter.Print(writer.ToString());
            }
        }

        IGgpDebugEngine TryGetDebugEngine()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var debugEngines = debugEngineManager.GetDebugEngines();
            // TODO: Provide a mechanism for the client to pick which debugger to dispatch
            // the command to.
            if (!debugEngines.Any())
            {
                commandWindowWriter.Print("Failed to find an active debug session.");
                Trace.WriteLine("Unable to execute command. No DebugEngine found.");
                return null;
            }
            if (debugEngines.Count > 1)
            {
                var warningMsg = "WARNING: Multiple DebugEngines found.  Will execute command on " +
                    "the most recently started.";
                commandWindowWriter.Print(warningMsg);
                Trace.WriteLine(warningMsg);
            }
            return debugEngines.First();
        }
    }
}
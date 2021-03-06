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

﻿using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YetiCommon.SSH;

namespace YetiCommon
{
    // Contains methods for extracting info from elf binaries and symbol files.
    public class ElfFileUtil : IBinaryFileUtil
    {
        static readonly Regex _hexDumpRegex =
            new Regex(@"^\s*[0-9a-fA-F]+(?:\s([0-9a-fA-F]+)){1,4}");
        static readonly Regex _fileTruncatedRegex =
            new Regex(@"section table goes past the end of file");
        static readonly Regex _fileNotRecognizedRegex =
            new Regex(@"The file was not recognized as a valid object file");
        static readonly Regex _sectionTableStartRegex = new Regex(@"^Sections:");
        static readonly Regex _debugInfoSectionRegex = new Regex(@"^[ 0-9]*\.debug_info ");

        JoinableTaskFactory taskFactory;
        ManagedProcess.Factory processFactory;

        public ElfFileUtil(JoinableTaskFactory taskFactory, ManagedProcess.Factory processFactory)
        {
            this.taskFactory = taskFactory;
            this.processFactory = processFactory;
        }

        /// <summary>
        /// Parses an elf binary or symbol file and returns the build ID encoded
        /// in the .note.gnu.build-id section of the file.
        /// </summary>
        /// <param name="filepath">The local or remote absolute file path.</param>
        /// <param name="target">Optional parameter specifying the remote gamelet.</param>
        /// <returns>A non-empty build id.</returns>
        /// <exception cref="BinaryFileUtilException">
        /// Thrown when an error is encountered reading or parsing the build id.
        /// InnerException contains more details.
        /// </exception>
        public BuildId ReadBuildId(string filepath, SshTarget target = null)
        {
            return taskFactory.Run(async () => await ReadBuildIdAsync(filepath, target));
        }

        public async Task<BuildId> ReadBuildIdAsync(string filepath, SshTarget target = null)
        {
            try
            {
                var outputLines = await ReadSectionFromFileAsync(".note.gnu.build-id", filepath,
                    target);
                var hexString = ParseHexDump(outputLines);
                var result = ParseBuildIdOutput(hexString);
                if (result == BuildId.Empty)
                {
                    throw new InvalidBuildIdException(
                        ErrorStrings.FailedToReadBuildId(filepath,
                            ErrorStrings.EmptyBuildId));
                }
                return result;
            }
            catch (ProcessExecutionException e)
            {
                LogObjdumpOutput(e);

                // objdump returned an error code, possibly because the file being parsed is not
                // actually an elf file. With an SSH target, exit code 255 means SSH failed before
                // it had a chance to execute the remote command.
                if (target != null && e.ExitCode < 255)
                {
                    // The remote command failed, so we need to fix the exception message.
                    // TODO: ManagedProcess should report the remote filename.
                    throw new BinaryFileUtilException(
                        ErrorStrings.FailedToReadBuildId(
                            filepath, ErrorStrings.ProcessExitedWithErrorCode(
                                YetiConstants.ObjDumpLinuxExecutable, e.ExitCode)),
                        e);
                }
                else
                {
                    throw new BinaryFileUtilException(
                        ErrorStrings.FailedToReadBuildId(filepath, e.Message), e);
                }
            }
            catch (ProcessException e)
            {
                // objdump failed to launch, possibly because the SDK was not found. With an SSH
                // target, this indicates that SSH failed to launch. In either case, the specific
                // filepath was never accessed, so it is not part of the error.
                throw new BinaryFileUtilException(
                    ErrorStrings.FailedToReadBuildId(e.Message), e);
            }
            catch (FormatException e)
            {
                // Indicates the build ID section is malformed.
                throw new InvalidBuildIdException(
                    ErrorStrings.FailedToReadBuildId(
                        filepath, ErrorStrings.MalformedBuildID),
                    e);
            }
        }

        /// <summary>
        /// Parses an elf binary or symbol file and returns the debug symbol file directory encoded
        /// in the .note.debug_info_dir section of the file.
        /// </summary>
        /// <param name="filepath">The local file path.</param>
        /// <returns>A non-empty string.</returns>
        /// <exception cref="BinaryFileUtilException">
        /// Thrown when an error is encountered reading or parsing the debug symbol file directory.
        /// InnerException contains more details.
        /// </exception>
        public string ReadSymbolFileDir(string filepath)
        {
            return taskFactory.Run(async () => await ReadSymbolFileDirAsync(filepath));
        }

        async Task<string> ReadSymbolFileDirAsync(string filepath)
        {
            try
            {
                List<string> outputLines =
                    await ReadSectionFromFileAsync(".note.debug_info_dir", filepath, null);
                var hexString = ParseHexDump(outputLines);
                var symbolFileDir = ParseObjdumpHexString(hexString);
                if (string.IsNullOrEmpty(symbolFileDir))
                {
                    throw new BinaryFileUtilException(
                        ErrorStrings.FailedToReadSymbolFileDir(filepath, ErrorStrings.NoDebugDir));
                }
                return symbolFileDir;
            }
            catch (ProcessExecutionException e)
            {
                LogObjdumpOutput(e);

                // objdump returned a non-zero exit code.
                throw new BinaryFileUtilException(
                    ErrorStrings.FailedToReadSymbolFileDir(filepath, e.Message), e);
            }
            catch (ProcessException e)
            {
                // objdump failed to launch. The specific filepath was never accessed,
                // so it is not part of the error.
                throw new BinaryFileUtilException(ErrorStrings.FailedToReadSymbolFileDir(e.Message),
                                                  e);
            }
            catch (FormatException e)
            {
                throw new BinaryFileUtilException(ErrorStrings.FailedToReadSymbolFileDir(
                                                      filepath, ErrorStrings.MalformedDebugDir),
                                                  e);
            }
        }

        /// <summary>
        /// Parses an elf binary or symbol file and returns the debug symbol file name encoded
        /// in the .gnu_debuglink section of the file.
        /// </summary>
        /// <param name="filepath">The local file path.</param>
        /// <returns>A non-empty string.</returns>
        /// <exception cref="BinaryFileUtilException">
        /// Thrown when an error is encountered reading or parsing the debug symbol file name.
        /// InnerException contains more details.
        /// </exception>
        public string ReadSymbolFileName(string filepath)
        {
            return taskFactory.Run(async () => await ReadSymbolFileNameAsync(filepath));
        }

        async Task<string> ReadSymbolFileNameAsync(string filepath)
        {
            List<string> outputLines;
            try
            {
                outputLines = await ReadSectionFromFileAsync(".gnu_debuglink", filepath, null);
                var hexString = ParseHexDump(outputLines);
                var symbolFileName = ParseObjdumpHexString(hexString);
                if (string.IsNullOrEmpty(symbolFileName))
                {
                    throw new BinaryFileUtilException(
                        ErrorStrings.FailedToReadSymbolFileName(
                            filepath, ErrorStrings.NoDebugLink));
                }
                return symbolFileName;
            }
            catch (ProcessExecutionException e)
            {
                LogObjdumpOutput(e);

                // objdump returned a non-zero exit code
                throw new BinaryFileUtilException(
                    ErrorStrings.FailedToReadSymbolFileName(filepath, e.Message), e);
            }
            catch (ProcessException e)
            {
                // objdump failed to launch. The specific filepath was never accessed,
                // so it is not part of the error.
                throw new BinaryFileUtilException(
                    ErrorStrings.FailedToReadSymbolFileName(e.Message), e);
            }
            catch (FormatException e)
            {
                // Indicates the debug link section is malformed.
                throw new BinaryFileUtilException(
                    ErrorStrings.FailedToReadSymbolFileName(
                        filepath, ErrorStrings.MalformedDebugLink),
                    e);
            }
        }

        public void VerifySymbolFile(string filepath, bool isDebugInfoFile)
        {
            taskFactory.Run(async () => await VerifySymbolFileAsync(filepath, isDebugInfoFile));
        }

        async Task VerifySymbolFileAsync(string filepath, bool isDebugInfoFile)
        {
            try
            {
                var outputLines = await ReadSectionHeaderListFromFileAsync(filepath);

                if (!isDebugInfoFile)
                {
                    // If we are not to check the presence of .debug_info, it is sufficient that
                    // reading the sections succeeded.
                    return;
                }

                // Verify that the file has a .debug_info section in the section table.
                bool foundSectionTableStart = false;
                foreach (string line in outputLines)
                {
                    if (foundSectionTableStart)
                    {
                        if (_debugInfoSectionRegex.IsMatch(line))
                        {
                            // We found the .debug_info section, let us declare success.
                            return;
                        }
                    }
                    else if (_sectionTableStartRegex.IsMatch(line))
                    {
                        foundSectionTableStart = true;
                    }
                }

                // If we found the section table but did not find .debug_info, report an error.
                if (foundSectionTableStart)
                {
                    throw new BinaryFileUtilException(
                        ErrorStrings.MissingDebugInfoInSymbolFile(filepath));
                }

                // If we did not find the section table, let us log this.
                Trace.WriteLine($"Internal error: Could not find a section table in llvm-objdump " +
                                " output for '{filepath}'. Output of objdump:");
                Trace.WriteLine(Environment.NewLine +
                                string.Join(Environment.NewLine, outputLines));
            }
            catch (ProcessExecutionException e)
            {
                LogObjdumpOutput(e);

                // objdump returned an error code, possibly because the file being parsed is not
                // actually an elf file.
                // Let us handle some common errors specially to produce better error messages.
                if (e.ErrorLines.Count > 0)
                {
                    if (_fileTruncatedRegex.IsMatch(e.ErrorLines[0]))
                    {
                        throw new BinaryFileUtilException(
                            ErrorStrings.SymbolFileTruncated(filepath), e);
                    }
                    else if (_fileNotRecognizedRegex.IsMatch(e.ErrorLines[0]))
                    {
                        throw new BinaryFileUtilException(
                            ErrorStrings.InvalidSymbolFileFormat(filepath), e);
                    }
                }
                throw new BinaryFileUtilException(
                    ErrorStrings.FailedToReadSymbolFile(filepath, e.Message), e);
            }
            catch (ProcessException e)
            {
                // objdump failed to launch, possibly because the SDK was not found.
                throw new BinaryFileUtilException(
                    ErrorStrings.FailedToReadSymbolFile(filepath, e.Message), e);
            }
        }

        /// <summary>
        /// Parse a local ELF file and return its list of headers.
        /// </summary>
        async Task<List<string>> ReadSectionHeaderListFromFileAsync(string filepath)
        {
            var objDumpArgs = "--section-headers " + ProcessUtil.QuoteArgument(filepath);
            ProcessStartInfo startInfo;
            string objDumpPath =
                Path.Combine(YetiConstants.LldbDir, "bin", YetiConstants.ObjDumpWinExecutable);
            startInfo = new ProcessStartInfo(objDumpPath, objDumpArgs);
            using (var process = processFactory.Create(startInfo))
            {
                return await process.RunToExitWithSuccessCapturingOutputAsync();
            }
        }

        /// <summary>
        /// Parse an ELF file (local or remote) and return the content of the specified section.
        /// </summary>
        /// <param name="section">The name of the section to read.</param>
        /// <param name="filepath">The absolute path to the ELF file.</param>
        /// <param name="target">
        /// Gamelet target to remotely read file from. If null, then read a local file.
        /// </param>
        /// <returns>All lines of the 'objdump' process output, not including newlines.</returns>
        async Task<List<string>> ReadSectionFromFileAsync(string section, string filepath,
            SshTarget target)
        {
            var objDumpArgs = $"-s --section={section}" + " " +
                ProcessUtil.QuoteArgument(filepath);
            ProcessStartInfo startInfo;
            if (target != null)
            {
                // TODO: use llvm-objdump on remotely, when it becomes available.
                startInfo = ProcessStartInfoBuilder.BuildForSsh(
                    $"{YetiConstants.ObjDumpLinuxExecutable} {objDumpArgs}",
                    new List<string>(),
                    target);
            }
            else
            {
#if USE_LOCAL_PYTHON_AND_TOOLCHAIN
                // This is gated by the <DeployPythonAndToolchainDependencies> project setting to
                // speed up the build.
                string toolchainDir = File.ReadAllText(
                    Path.Combine(YetiConstants.RootDir, "local_toolchain_dir.txt")).Trim();
                string objDumpPath = Path.Combine(toolchainDir, "windows", "bin",
                                                  YetiConstants.ObjDumpWinExecutable);

                // Quick sanity check that the file exist.
                if (!File.Exists(objDumpPath))
                {
                    // Note: This error is only shown to internal VSI devs, not to external devs.
                    throw new BinaryFileUtilException(
                        "You have set the <DeployPythonAndToolchainDependencies> project setting" +
                        $" to False to speed up deployment, but the objdump file {objDumpPath} " +
                        "moved. Either fix the wrong file path (preferred) or set " +
                        "<DeployPythonAndToolchainDependencies> to False.");
                }
#else
                string objDumpPath =
                    Path.Combine(YetiConstants.LldbDir, "bin", YetiConstants.ObjDumpWinExecutable);
#endif
                startInfo = new ProcessStartInfo(objDumpPath, objDumpArgs);
            }
            using (var process = processFactory.Create(startInfo))
            {
                return await process.RunToExitWithSuccessCapturingOutputAsync();
            }
        }

        private void LogObjdumpOutput(ProcessExecutionException e)
        {
            Trace.WriteLine("objdump invocation failed" + Environment.NewLine +
                "stdout: " + Environment.NewLine +
                string.Join(Environment.NewLine, e.OutputLines) + Environment.NewLine +
                "stderr:" + Environment.NewLine +
                string.Join(Environment.NewLine, e.ErrorLines));
        }

        /// <summary>
        /// Given the content of the build ID section, returns the build ID.
        /// </summary>
        /// <param name="hexString">The content of the section represented in hex.</param>
        /// <returns>A valid but possibly empty build ID</returns>
        /// <exception cref="FormatException">
        /// Thrown when the input does not have enough leading bytes, or when it does not encode a
        /// valid build ID.
        /// </exception>
        BuildId ParseBuildIdOutput(string hexString)
        {
            // A note segment consists of a 4 byte namesz field, a 4 byte descsz field, a 4 byte
            // type field, a namesz-length name field, and a descsz-length desc field.
            // In the case of the gnu.build-id segment, name is a 4 byte string with the contents
            // "GNU\0", and desc is the actual build ID. All together, there are 16 bytes
            // preceding the actual build ID, which makes for 32 hex digits that we want to skip.
            if (hexString.Length < 32)
            {
                throw new FormatException(
                    $"Got {hexString.Length} hex digits, but wanted at least 32 leading digits");
            }

            return new BuildId(hexString.Substring(32));
        }

        /// <summary>
        /// Given the hex-encoded content of a section with a string (such as debuglink),
        /// this method decodes and returns the string.
        /// </summary>
        /// <param name="hexString">The content of the section represented in hex.</param>
        /// <returns>A valid ASCII string or the empty string.</returns>
        /// <exception cref="FormatException">
        /// Thrown when the input does not encode a valid ASCII string.
        /// </exception>
        string ParseObjdumpHexString(string hexString)
        {
            // Convert a string of hex digits into an array of raw byte values.
            var rawBytes = PairChars(hexString).Select(s => Convert.ToByte(s, 16));

            // Interpret rawBytes as a zero-terminated ASCII character string.
            // TODO: figure out Unicode support.
            return Encoding.ASCII.GetString(rawBytes.TakeWhile(b => b != 0).ToArray());
        }

        /// <summary>
        /// Parses a hex dump in the format outputted by objdump, and returns just the hex digits.
        /// </summary>
        /// <param name="hexDumpOutput">The raw output of the 'objdump' process.</param>
        /// <returns>The hexadecimal characters concatenated together without whitespace.</returns>
        string ParseHexDump(IList<string> hexDumpOutput)
        {
            var hexString = new StringBuilder();
            foreach (var line in hexDumpOutput)
            {
                var match = _hexDumpRegex.Match(line);
                foreach (Capture capture in match.Groups[1].Captures)
                {
                    hexString.Append(capture.Value);
                }
            }
            return hexString.ToString();
        }

        /// <summary>
        /// Pairs up adjacent characters in a string: "abcdef" => {"ab", "cd", "ef"}.
        /// </summary>
        /// <param name="input">A string with an even length (possibly empty).</param>
        /// <returns>A sequence of strings (possibly empty); each element has length 2.</returns>
        static IEnumerable<string> PairChars(string input)
        {
            if (input.Length % 2 != 0)
            {
                throw new FormatException($"Got {input.Length} digits, but wanted an even length");
            }
            for (var i = 0; i < input.Length; i += 2)
            {
                yield return input.Substring(i, 2);
            }
        }
    }
}

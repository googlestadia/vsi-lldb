// Copyright 2022 Google LLC
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
using YetiCommon.SSH;

namespace YetiCommon
{
    public class ModuleParser : IModuleParser
    {
        readonly string _debugLinkName = ".gnu_debuglink";
        readonly string _debugDirName = ".note.debug_info_dir";
        readonly string _buildIdName = ".note.gnu.build-id";
        readonly string _debugInfoName = ".debug_info";
        static readonly Regex _hexDumpRegex =
            new Regex(@"^\s*[0-9a-fA-F]+(?:\s([0-9a-fA-F]+)){1,4}");

        public DebugLinkLocationInfo ParseDebugLinkInfo(string filepath)
        {
            var output = new DebugLinkLocationInfo();
            if (!File.Exists(filepath))
            {
                output.AddError($"{filepath} not found");
                return output;
            }

            if (!ELFReader.TryLoad(filepath, out IELF elfReader))
            {
                output.AddError(ErrorStrings.InvalidSymbolFileFormat(filepath));
                return output;
            }

            using (elfReader)
            {
                if (elfReader.TryGetSection(_debugDirName, out ISection directorySection))
                {
                    byte[] contents = directorySection.GetContents();
                    output.SetDirectory(ParseStringValue(contents));
                }
                else
                {
                    output.AddError(ErrorStrings.FailedToReadSymbolFileDir(
                                        filepath, ErrorStrings.NoDebugDir));
                }

                if (elfReader.TryGetSection(_debugLinkName, out ISection debugLinkSection))
                {
                    byte[] contents = debugLinkSection.GetContents();
                    output.SetFilename(ParseStringValue(contents));
                }
                else
                {
                    output.AddError(ErrorStrings.FailedToReadSymbolFileName(
                                        filepath, ErrorStrings.NoDebugLink));
                }
            }

            return output;
        }

        public BuildIdInfo ParseBuildIdInfo(string filepath, bool isElf)
        {
            var output = new BuildIdInfo();
            if (!File.Exists(filepath))
            {
                output.AddError($"{filepath} not found");
                return output;
            }

            if (isElf)
            {
                ParseBuildIdFromElf(filepath, ref output);
            }
            else
            {
                // TODO: add PE-modules processing
                output.AddError("Cannot read BuildId from PE module");
            }

            return output;
        }

        void ParseBuildIdFromElf(string filepath, ref BuildIdInfo output)
        {
            if (!ELFReader.TryLoad(filepath, out IELF elfReader))
            {
                output.AddError(ErrorStrings.InvalidSymbolFileFormat(filepath));
                return;
            }

            using (elfReader)
            {
                if (!elfReader.TryGetSection(_buildIdName, out ISection buildIdSection))
                {
                    output.AddError(
                        ErrorStrings.FailedToReadBuildId(filepath, ErrorStrings.EmptyBuildId));
                    return;
                }

                if (buildIdSection is INoteSection buildIdNoteSection)
                {
                    byte[] contents = buildIdNoteSection.Description;
                    output.Data = ParseBuildIdValue(contents);
                }
                else
                {
                    output.AddError(
                        ErrorStrings.FailedToReadBuildId(filepath, ErrorStrings.EmptyBuildId));
                }
            }
        }

        public async Task<BuildId> ParseRemoteBuildIdInfoAsync(string filepath, SshTarget target)
        {
            if (target == null)
            {
                throw new BinaryFileUtilException(ErrorStrings.FailedToReadBuildId(filepath,
                                                      "Remote target was not provided"));
            }

            string objDumpArgs =
                $"-s --section={_buildIdName} {ProcessUtil.QuoteArgument(filepath)}";
            ProcessStartInfo startInfo = ProcessStartInfoBuilder.BuildForSsh(
                $"{YetiConstants.ObjDumpLinuxExecutable} {objDumpArgs}",
                new List<string>(),
                target);
            var processFactory = new ManagedProcess.Factory();
            try
            {
                List<string> outputLines;
                using (IProcess process = processFactory.Create(startInfo))
                {
                    outputLines = await process.RunToExitWithSuccessCapturingOutputAsync();
                }

                string hexString = ParseHexDump(outputLines);
                BuildId buildId = ParseBuildIdOutput(hexString);
                return buildId;
            }
            catch (ProcessExecutionException e)
            {
                LogObjdumpOutput(e);

                // objdump returned an error code, possibly because the file being parsed is not
                // actually an elf file. With an SSH target, exit code 255 means SSH failed
                // before it had a chance to execute the remote command.
                if (e.ExitCode < 255)
                {
                    // The remote command failed, so we need to fix the exception message.
                    // TODO: ManagedProcess should report the remote filename.
                    throw new BinaryFileUtilException(
                        ErrorStrings.FailedToReadBuildId(
                            filepath, ErrorStrings.ProcessExitedWithErrorCode(
                                YetiConstants.ObjDumpLinuxExecutable, e.ExitCode)),
                        e);
                }

                throw new BinaryFileUtilException(
                    ErrorStrings.FailedToReadBuildId(filepath, e.Message), e);
            }
            catch (ProcessException e)
            {
                // objdump failed to launch, possibly because the SDK was not found. With an SSH
                // target, this indicates that SSH failed to launch. In either case, the specific
                // filepath was never accessed, so it is not part of the error.
                throw new BinaryFileUtilException(
                    ErrorStrings.FailedToReadBuildId(filepath, e.Message), e);
            }
            catch (FormatException e)
            {
                // Indicates the build ID section is malformed.
                throw new InvalidBuildIdException(
                    ErrorStrings.FailedToReadBuildId(
                        filepath, ErrorStrings.MalformedBuildId),
                    e);
            }

            void LogObjdumpOutput(ProcessExecutionException e)
            {
                Trace.WriteLine("objdump invocation failed \nstdout: \n" +
                                string.Join("\n", e.OutputLines) +
                                "\nstderr: \n" +
                                string.Join("\n", e.ErrorLines));
            }
        }

        /// <summary>
        /// Parses a hex dump in the format outputted by objdump, and returns just the hex digits.
        /// </summary>
        /// <param name="hexDumpOutput">The raw output of the 'objdump' process.</param>
        /// <returns>The hexadecimal characters concatenated together without whitespace.</returns>
        string ParseHexDump(IList<string> hexDumpOutput)
        {
            var hexString = new StringBuilder();
            foreach (string line in hexDumpOutput)
            {
                Match match = _hexDumpRegex.Match(line);
                foreach (Capture capture in match.Groups[1].Captures)
                {
                    hexString.Append(capture.Value);
                }
            }
            return hexString.ToString();
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
            // A note segment consists of a 4 byte namesz field, a 4 byte descsz field,
            // a 4 byte type field, a namesz-length name field, and a descsz-length desc field.
            // In the case of the gnu.build-id segment, name is a 4 byte string with the
            // contents "GNU\0", and desc is the actual build ID. All together, there are
            // 16 bytes preceding the actual build ID, which makes for 32 hex digits that we
            // want to skip.
            if (hexString.Length < 32)
            {
                throw new FormatException(
                    $"Got {hexString.Length} hex digits, " +
                    "but wanted at least 32 leading digits");
            }

            var buildId = new BuildId(hexString.Substring(32));

            if (buildId == BuildId.Empty)
            {
                throw new FormatException(ErrorStrings.EmptyBuildId);
            }

            return buildId;
        }

        public bool IsValidElf(string filepath, bool isDebugInfoFile, out string errorMessage)
        {
            errorMessage = "";
            if (!File.Exists(filepath))
            {
                errorMessage = $"{filepath} not found";
                return false;
            }

            if (!ELFReader.TryLoad(filepath, out IELF elfReader))
            {
                return false;
            }

            using (elfReader)
            {
                if (!isDebugInfoFile)
                {
                    // If we are not to check the presence of .debug_info, it is sufficient that
                    // the file has a valid ELF format.
                    return true;
                }

                if (elfReader.TryGetSection(_debugInfoName, out ISection buildIdSection))
                {
                    return true;
                }

                errorMessage += ErrorStrings.MissingDebugInfoInSymbolFile(filepath);
            }

            return false;
        }

        public string ParseStringValue(byte[] contents)
        {
            IEnumerable<byte> stringBytes = contents.TakeWhile(x => x != 0);
            return Encoding.ASCII.GetString(stringBytes.ToArray());
        }

        public BuildId ParseBuildIdValue(byte[] contents) => new BuildId(contents);
    }
}
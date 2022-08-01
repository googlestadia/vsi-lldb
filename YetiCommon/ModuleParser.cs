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
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
using SharpPdb.Managed;
using YetiCommon.SSH;

namespace YetiCommon
{
    public class ModuleParser : IModuleParser
    {
        readonly string _debugLinkName = ".gnu_debuglink";
        readonly string _debugDirName = ".note.debug_info_dir";
        readonly string _buildIdName = ".note.gnu.build-id";
        readonly string _debugInfoName = ".debug_info";
        readonly string _fileFormat = "file format";

        static readonly Regex _hexDumpRegex =
            new Regex(@"^\s*[0-9a-fA-F]+(?:\s([0-9a-fA-F]+)){1,4}");

        public DebugLinkLocationInfo ParseDebugLinkInfo(string filepath)
        {
            var output = new DebugLinkLocationInfo();
            if (!File.Exists(filepath))
            {
                output.Data = SymbolFileLocation.Empty;
                output.AddError($"{filepath} not found");
                return output;
            }

            if (!ELFReader.TryLoad(filepath, out IELF elfReader))
            {
                output.Data = SymbolFileLocation.Empty;
                output.AddError(ErrorStrings.InvalidSymbolFileFormat(filepath, ModuleFormat.Elf));
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
                    output.Data = SymbolFileLocation.Empty;
                    output.AddError(ErrorStrings.FailedToReadSymbolFileName(
                                        filepath, ErrorStrings.NoDebugLink));
                }
            }

            return output;
        }

        public BuildIdInfo ParseBuildIdInfo(string filepath, ModuleFormat format)
        {
            var output = new BuildIdInfo();
            if (!File.Exists(filepath))
            {
                output.AddError($"{filepath} not found");
                return output;
            }

            switch (format)
            {
                case ModuleFormat.Elf:
                    return ParseBuildIdFromElf(filepath);
                case ModuleFormat.Pdb:
                    return ParseBuildIdFromPdb(filepath);
                case ModuleFormat.Pe:
                    return ParseBuildIdFromPe(filepath);
                default:
                    output.AddError($"Unknown format {format}");
                    return output;
            }
        }

        BuildIdInfo ParseBuildIdFromElf(string filepath)
        {
            var output = new BuildIdInfo();
            if (!ELFReader.TryLoad(filepath, out IELF elfReader))
            {
                output.AddError(ErrorStrings.InvalidSymbolFileFormat(filepath, ModuleFormat.Elf));
                return output;
            }

            using (elfReader)
            {
                if (!elfReader.TryGetSection(_buildIdName, out ISection buildIdSection))
                {
                    output.AddError(
                        ErrorStrings.FailedToReadBuildId(filepath, ErrorStrings.EmptyBuildId));
                    return output;
                }

                if (buildIdSection is INoteSection buildIdNoteSection)
                {
                    byte[] contents = buildIdNoteSection.Description;
                    output.Data = ParseBuildIdValue(contents, ModuleFormat.Elf);
                }
                else
                {
                    output.AddError(
                        ErrorStrings.FailedToReadBuildId(filepath, ErrorStrings.EmptyBuildId));
                }
            }

            return output;
        }

        BuildIdInfo ParseBuildIdFromPdb(string filepath)
        {
            var output = new BuildIdInfo();
            using (IPdbFile pdb = PdbFileReader.OpenPdb(filepath))
            {
                if (pdb == null)
                {
                    output.AddError(
                        ErrorStrings.InvalidSymbolFileFormat(filepath, ModuleFormat.Pdb));
                    return output;
                }

                output.Data = GetBuildId(pdb.Guid, pdb.Age, ModuleFormat.Pdb);
            }

            return output;
        }

        BuildIdInfo ParseBuildIdFromPe(string filepath)
        {
            var output = new BuildIdInfo();
            try
            {
                using (FileStream fileStream = File.OpenRead(filepath))
                {
                    using (var reader = new PEReader(fileStream))
                    {
                        try
                        {
                            // When accessing PEHeaders a BadImageFormatException might be thrown
                            // if the file 'filepath' is malformed (PEHeaders is lazily initialized).
                            if (reader.PEHeaders.PEHeader == null)
                            {
                                output.AddError(ErrorStrings.InvalidSymbolFileFormat(
                                                    filepath, ModuleFormat.Pe));
                                return output;
                            }
                        }
                        catch (BadImageFormatException)
                        {
                            output.AddError(ErrorStrings.InvalidSymbolFileFormat(
                                                filepath, ModuleFormat.Pe));
                            return output;
                        }

                        foreach (DebugDirectoryEntry entry in reader.ReadDebugDirectory())
                        {
                            if (entry.Type != DebugDirectoryEntryType.CodeView)
                            {
                                continue;
                            }

                            CodeViewDebugDirectoryData cv =
                                reader.ReadCodeViewDebugDirectoryData(entry);
                            output.Data = GetBuildId(cv.Guid, cv.Age, ModuleFormat.Pe);
                            return output;
                        }
                    }
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException ||
                e is NotSupportedException || e is ArgumentException ||
                e is ObjectDisposedException)
            {
                output.AddError($"Error opening {filepath}: {e.Message}");
            }

            return output;
        }

        BuildId GetBuildId(Guid guid, int age, ModuleFormat moduleFormat) =>
            new BuildId($"{guid}-{age:X8}", moduleFormat);

        public async Task<BuildId> ParseRemoteBuildIdInfoAsync(string filepath, SshTarget target)
        {
            if (target == null)
            {
                throw new BinaryFileUtilException(
                    ErrorStrings.FailedToReadBuildId(filepath, "Remote target was not provided"));
            }

            string objDumpArgs =
                $"-s --section={_buildIdName} {ProcessUtil.QuoteArgument(filepath)}";
            ProcessStartInfo startInfo = ProcessStartInfoBuilder.BuildForSsh(
                $"{YetiConstants.ObjDumpLinuxExecutable} {objDumpArgs}", new List<string>(),
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
                ModuleFormat moduleFormat = ParseModuleFormatDump(outputLines);
                BuildId buildId = ParseBuildIdOutput(hexString, moduleFormat);
                return buildId;
            }
            catch (ProcessExecutionException e)
            {
                LogObjdumpOutput(e);

                // objdump returned an error code, possibly because the file being parsed does not
                // exist or is not actually an elf file. With an SSH target, exit code 255 means SSH
                // failed before it had a chance to execute the remote command.
                if (e.ExitCode < 255)
                {
                    // E.g. objdump: '/mnt/developer/foo': No such file
                    if (e.OutputLines.Any(l => l.Contains(filepath) && l.Contains("No such file")))
                    {
                        // Wrap into a FileNotFoundException inner exception if file was not found.
                        throw new BinaryFileUtilException($"Remote file {filepath} not found",
                                                          new FileNotFoundException());
                    }

                    // The remote command failed, so we need to fix the exception message.
                    // TODO: ManagedProcess should report the remote filename.
                    throw new BinaryFileUtilException(
                        ErrorStrings.FailedToReadBuildId(
                            filepath,
                            ErrorStrings.ProcessExitedWithErrorCode(
                                YetiConstants.ObjDumpLinuxExecutable, e.ExitCode)), e);
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
                    ErrorStrings.FailedToReadBuildId(filepath, e.Message), e);
            }

            void LogObjdumpOutput(ProcessExecutionException e)
            {
                Trace.WriteLine("objdump invocation failed \nstdout: \n" +
                                string.Join("\n", e.OutputLines) + "\nstderr: \n" +
                                string.Join("\n", e.ErrorLines));
            }
        }

        /// <summary>
        /// Parses a hex dump in the format outputted by objdump, and returns just the hex digits.
        /// </summary>
        /// <param name="hexDumpOutput">The raw output of the 'objdump' process.</param>
        /// <returns>The hexadecimal characters concatenated together without whitespace.</returns>
        public string ParseHexDump(IList<string> hexDumpOutput)
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
        /// Parses the module format specified in the objdump.
        /// </summary>
        /// <param name="outputLines"></param>
        /// <returns>The module format specified in the objdump lines. Currently we only support
        /// elf format.</returns>
        /// <exception cref="FormatException">If it's not possible to parse the module format.
        /// </exception>
        public ModuleFormat ParseModuleFormatDump(IList<string> outputLines)
        {
            foreach (string line in outputLines)
            {
                var index = line.IndexOf(_fileFormat, StringComparison.OrdinalIgnoreCase);
                if (index != -1)
                {
                    var formatSubstring = line.Substring(index + _fileFormat.Length);
                    if (formatSubstring.Contains("elf"))
                    {
                        return ModuleFormat.Elf;
                    }
                }
            }

            // TODO: Investigate how the buildId is handled in Silenus processes

            throw new FormatException(ErrorStrings.FailedToParseModuleFormatFromDump(outputLines));
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
        BuildId ParseBuildIdOutput(string hexString, ModuleFormat moduleFormat)
        {
            // A note segment consists of a 4 byte namesz field, a 4 byte descsz field,
            // a 4 byte type field, a namesz-length name field, and a descsz-length desc field.
            // In the case of the gnu.build-id segment, name is a 4 byte string with the
            // contents "GNU\0", and desc is the actual build ID. All together, there are
            // 16 bytes preceding the actual build ID, which makes for 32 hex digits that we
            // want to skip.
            if (hexString.Length < 32)
            {
                throw new FormatException($"Got {hexString.Length} hex digits, " +
                                          "but wanted at least 32 leading digits");
            }

            var buildId = new BuildId(hexString.Substring(32), moduleFormat);

            if (BuildId.IsNullOrEmpty(buildId))
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

                if (elfReader.TryGetSection(_debugInfoName, out ISection _))
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

        public BuildId ParseBuildIdValue(byte[] contents, ModuleFormat moduleFormat) =>
            new BuildId(contents, moduleFormat);
    }
}
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
using System.Linq;

namespace YetiCommon
{
    // Central location for error strings in YetiCommon that may be displayed to the user,
    // sorted alphabetically.
    public static class ErrorStrings
    {
        public const string EmptyBuildId = "The file must have a non-empty build ID.";
        public const string MalformedBuildID = "The build-id section is malformed.";
        public const string MalformedDebugLink = "The debuglink section is malformed.";
        public const string MalformedDebugDir = "The debug_info_dir section is malformed.";
        public const string NoDebugLink = "Unable to extract the file's debuglink section.";
        public const string NoDebugDir = "Unable to extract the file's debug_info_dir section.";
        public const string QueryParametersWrongFormat =
            "The 'Custom Query Parameters' value is in a wrong format and will be ignored. " +
            " The setting should be in the form " +
            "'{param_1}={value_1}&{param_2=value_2}&{param_n=value_n}'. " +
            "Please edit the setting in 'Project Properties' -> " +
            "'Debugging' -> 'Custom Query Parameters'.";
        public static string ErrorWaitingForProcess(string process, string message) =>
            $"Error waiting for {process} to exit. {message}";
        public static string FailedToCreateSshDirectory(string message) =>
            $"Failed to create ssh directory. {message}";
        public static string FailedToCreateSshKeysDirectory(string message) =>
            $"Failed to create SSH keys directory. {message}";
        public static string FailedToLaunchProcess(string fileName, string message) =>
            $"Failed to launch {fileName}. {message}";
        public static string FailedToReadSshKeyFile(string message) =>
            $"Failed to read SSH key file. {message}";
        public static string FailedToRunSshKeyGeneration(string message) =>
            $"Failed to generate SSH key. {message}";
        public static string FailedToSetJobLimitInfo(int errorCode) =>
            "Failed to set job limit info. The Windows API 'SetInformationJobObject' returned " +
            $"{errorCode}.";
        public static string FailedToReadBuildId(string message) =>
            $"Failed to read build ID. {message}";
        public static string FailedToReadBuildId(string filepath, string message) =>
            $"Failed to read build ID of '{filepath}'. {message}";
        public static string InvalidSymbolFileFormat(string filepath) =>
            $"Failed to load '{filepath}'. The file was not recognized as a valid ELF object file.";
        public static string MissingDebugInfoInSymbolFile(string filepath) =>
            $"Failed to load '{filepath}'. " +
            "The file does not contain a debug information (.debug_info) section.";
        public static string SymbolFileTruncated(string filepath) =>
            $"Failed to load '{filepath}'. The file is corrupted, it appears to be truncated.";
        public static string FailedToReadSymbolFile(string filepath, string message) =>
            $"Failed to read symbol file '{filepath}'. {message}";
        public static string FailedToReadSymbolFileName(string message) =>
            $"Failed to read symbol file name. {message}";
        public static string FailedToReadSymbolFileDir(string message) =>
            $"Failed to read symbol file directory. {message}";
        public static string FailedToReadSymbolFileName(string filepath, string message) =>
            $"Failed to read symbol file name of '{filepath}'. {message}";
        public static string FailedToReadSymbolFileDir(string filepath, string message) =>
            $"Failed to read symbol file directory of '{filepath}'. {message}";
        public static string FailedToWriteKnownHostsFile(string message) =>
            $"Failed to write known_hosts file. {message}";
        public static string ProcessExitedWithErrorCode(string process, int errorCode) =>
            $"{process} exited with error code {errorCode}.";
        public static string SshKeyGenerationFailed(string process, int errorCode) =>
            $"Failed to generate SSH key file. {process} exited with error code {errorCode}.";
        public static string TimeoutWaitingForProcess(string process) =>
            $"Timed out waiting for {process} to exit.";

        public static string InvalidEnvironmentVariable(string value) =>
            $"Invalid format of environment variable: '{value}'." +
            " Valid format: '<KEY>=<VALUE>'.";

        public static string MultipleEnvironmentVariableKeys(string key) =>
            $"The custom environment variable '{key}' is set multiple times. " +
            "The game will use the setting for this variable in " +
            "Custom Query Parameters (if it exists), " +
            "followed by any setting in Stadia Environment Variables (if it exists), " +
            "followed by any other setting that affects the variable.";

        public static string EnvironmentVariableOverride(string variableName) =>
            $"The custom environment variable '{variableName}' overrides the setting variable.";

        public const string EditEnvironmentVariables =
            "Either edit the setting in 'Project Properties' -> 'Debugging' -> " +
            "'Stadia Environment Variables', or remove the 'vars' parameter from " +
            "'Project Properties' -> 'Debugging' -> 'Custom Query Parameters'.";

        public static string QueryParametersIgnored(IEnumerable<string> parameters) =>
            "The following query parameters will be ignored � they can only be set via a VSI " +
            "setting or inferred from another configuration: " +
            string.Join(", ", parameters.Select(v => $"'{v}'")) + ".";

        public static string InvalidQueryParameterType(string parameterName, string value,
                                                       Type type) =>
            $"Can't convert query parameter's '{parameterName}' value '{value}' to {type}. " +
            $"The parameter '{parameterName}' will be ignored.";

        public static string InvalidEnumValue(string parameterName, string value,
                                              IEnumerable<string> expectedValues) =>
            $"The parameter '{parameterName}' has an invalid value: '{value}'. Valid " +
            $"values are: {string.Join(", ", expectedValues.Select(v => $"'{v}'"))}.";

        public static string InvalidBinaryName(string expected, string actual) =>
            "The query parameter 'cmd' has an invalid binary name: " + 
            $"{(string.IsNullOrWhiteSpace(actual) ? "an empty value" : $"'{actual}'")}. " +
            $"Expected: '{expected}'. " + Environment.NewLine +
            "To specify command line parameters for the binary, use the setting in " +
            "'Project Properties' -> 'Debugging' -> 'Stadia Launch Arguments'.";
    }
}

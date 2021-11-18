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

﻿using System;
using YetiCommon;

namespace SymbolStores
{
    // Central location for strings in SymbolStores that may be displayed to the user, sorted
    // alphabetically.
    public class Strings
    {
        public const string CopyToFlatStoreNotSupported =
            "Copying files to flat symbol directories is not supported.";
        public const string CopyToHttpStoreNotSupported =
            "Copying files to http symbol stores is not supported.";
        public const string CopyToStoreSequenceNotSupported =
            "Copying files to symbol store sequences is not supported.";
        public const string EmptyBuildId = "Build ID is unknown.";
        public const string FilenameNullOrEmpty = "Filename is null or empty.";
        public const string PathNullOrEmpty = "Path is null or empty.";
        public const string SourceFileReferenceNull = "Source file reference is null.";
        public const string UrlNullOrEmpty = "URL is null or empty.";

        public static string BuildIdMismatch(string filepath, BuildId expectedBuildId,
            BuildId actualBuildId) =>
            $"{filepath}... Build ID does not match. Expected build ID '${expectedBuildId}', " +
            $"file has build ID '${actualBuildId}'.";
        public static string ConnectionIsUnencrypted(string host) => $"Warning: The connection " +
            $"to '{host}' is unencrypted. Use HTTPS instead of HTTP for a more secure connection.";
        public static string CopiedFile(string filename, string filepath) =>
            $"Copied '{filename}' to '{filepath}'.";
        public static string FailedToCopyToStructuredStore(string path, string filename,
            string message) =>
            $"Could not copy '{filename}' to symbol store '{path}'. {message}";
        public static string FailedToCopyToSymbolServer(string filename) =>
            $"Could not copy '{filename}' to any store in the symbol server.";
        public static string FailedToCopyToSymbolServer(string filename, string message) =>
            $"Could not copy '{filename}' to the symbol server. {message}";
        public static string FailedToCreateFlatStore(string message) =>
            $"Failed to create flat symbol store. {message}";
        public static string FailedToCreateStructuredStore(string message) =>
            $"Failed to create structured symbol store. {message}";
        public static string FailedToSearchFlatStore(string path, string filename,
            string message) =>
            $"Could not search directory '{path}' for '{filename}'. {message}";
        public static string FailedToSearchHttpStore(string baseUrl, string filename,
            string message) =>
            $"Could not search http symbol store '{baseUrl}' for '{filename}'. {message}";
        public static string FailedToSearchStadiaStore(string filename, string message) =>
            $"Could not search Stadia symbol store for '{filename}'. {message}";
        public static string DoesNotExistInStadiaStore(string filename, string buildId) =>
            $"Symbol '{filename}' ({buildId}) won't be searched in Stadia store. You can " +
            "load it via `Modules->Load Symbols` if you believe that it exists in the store.";
        public static string DoesNotExistInHttpStore(string filename, string url) =>
            $"Symbol '{filename}' won't be searched in Http store '{url}'. You can " +
            "load it via `Modules->Load Symbols` if you believe that it exists in the store.";
        public static string FailedToSearchStadiaStoreHTTPStorage(string filename, int statusCode,
            string reason) =>
            $"Could not search Stadia symbol store for '{filename}'. " +
            $"Storage server returned HTTP status code {statusCode} ({reason}).";
        public static string FailedToSearchStructuredStore(string path, string filename,
            string message) =>
            $"Could not search symbol store '{path}' for '{filename}'. {message}";
        public static string FileAlreadyExists(string destPath) =>
            $"A file already exists at the destination path '{destPath}'.";
        public static string FileNotFound(string filepath) => $"{filepath}... File not found.";
        public static string FileNotFoundInHttpStore(string fileUrl, int statusCode,
            string reason) => $"{fileUrl}... File not found. Server returned HTTP status code " +
            $"{statusCode} ({reason}).";
        public static string FileNotFoundInStadiaStore(string buildlId, string filename) =>
            $"{buildlId}/{filename}... File not found.";
        public static string FileFound(string filepath) => $"{filepath}... File found.";
        public static string UnsupportedSymbolServer(string serverName) =>
            $"Unsupported symbol server '{serverName}'. Emulating symbol servers other than " +
            $"symsrv.dll is not supported.";
    }
}

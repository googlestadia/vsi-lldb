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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;

namespace YetiCommon
{
    public struct Versions
    {
        // Class that represents the version of an SDK.
        // Currently two formats are supported.
        //
        // Release versions. Which consist of 4 numbers separated by '.', the order of the numbers
        // is expected to be build, major, minor and patch.
        //   ex. 7489.1.24.0
        //
        // Master versions. Which consist of the build number followed by 'master'.
        //   ex. 8493.master
        public class SdkVersion
        {
            public string major { get; }
            public string minor { get; }
            public string patch { get; }
            public string build { get; }

            public static SdkVersion Create(string version)
            {
                var splitVersion = version.Split('.');
                switch (splitVersion.Length)
                {
                    case 2:
                        return new SdkVersion(splitVersion[1], string.Empty, string.Empty,
                            splitVersion[0]);
                    case 4:
                        return new SdkVersion(splitVersion[1], splitVersion[2], splitVersion[3],
                            splitVersion[0]);
                    default:
                        Trace.WriteLine("Unable to parse SDK version. Unrecognized format: " +
                            $"{version}");
                        return null;
                }
            }

            private SdkVersion(string major, string minor, string patch, string build)
            {
                this.major = major;
                this.minor = minor;
                this.patch = patch;
                this.build = build;
            }

            public override string ToString()
            {
                string version = major;
                if (!string.IsNullOrEmpty(minor))
                {
                    version += $".{minor}";
                }
                if (!string.IsNullOrEmpty(patch))
                {
                    version += $".{patch}";
                }
                if (!string.IsNullOrEmpty(build))
                {
                    version += $".{build}";
                }
                return version;
            }
        }

        // Populates a Versions struct using the local environment.
        // If a field cannot be populated, it is set to the empty string.
        public static Versions Populate(string vsVersion)
        {
            return new Versions
            {
                FullSdkVersionString = GetFullSdkVersionString(),
                ExtensionVersion = GetExtensionVersion(),
                VsVersion = vsVersion ?? "",
            };
        }

        // Attempts to read the SDK version file from the SDK path and extracts the version.
        // If the read or parsing failed, logs an error and returns the empty string.
        public static string GetFullSdkVersionString()
        {
            var sdkVersionFilePath = Path.Combine(SDKUtil.GetSDKPath(), "VERSION");
            try
            {
                return File.ReadLines(sdkVersionFilePath).First();
            }
            catch (Exception e) when (
                e is IOException ||
                e is UnauthorizedAccessException)
            {
                Trace.WriteLine($"Failed to read or parse VERSION file '{sdkVersionFilePath}'." +
                    $"{Environment.NewLine}{e}");
            }
            return "";
        }

        // Attempts to read the SDK version file from the SDK path and extracts the version.
        // If the read or parsing failed, logs an error and returns null.
        public static SdkVersion GetSdkVersion()
        {
            string fullVersion = GetFullSdkVersionString();
            if (string.IsNullOrEmpty(fullVersion))
                return null;

            string version = fullVersion.Split().First();
            return SdkVersion.Create(version);
        }

        /// <summary>
        /// Attempts to get the extension version from the vsixmanifest file.
        /// The path of the manifest can be overridden for testing.
        /// Returns null on failure.
        /// </summary>
        public static string GetExtensionVersion(string vsixManifestPathForTesting = null)
        {
            string vsixManifestPath = vsixManifestPathForTesting ??
                Path.Combine(YetiConstants.RootDir, YetiConstants.VsixManifest);
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(vsixManifestPath);
                XmlNamespaceManager namespaces = new XmlNamespaceManager(doc.NameTable);
                namespaces.AddNamespace(
                    "ns", "http://schemas.microsoft.com/developer/vsx-schema/2011");
                XmlNode identityNode =
                    doc.SelectSingleNode("/ns:PackageManifest/ns:Metadata/ns:Identity", namespaces);
                return identityNode.Attributes["Version"].Value;
            }
            catch (Exception e) when (
                e is IOException ||
                e is UnauthorizedAccessException)
            {
                Trace.WriteLine($"Failed to read or parse manifest file '{vsixManifestPath}'." +
                                $"{Environment.NewLine}{e}");
                return null;
            }
        }

        // SDK version found in the installed 'VERSION' file, or empty if SDK was not found.
        public string FullSdkVersionString { get; set; }
        // Extension version found in the VSIX manifest file, or empty if the file was not found.
        public string ExtensionVersion { get; set; }
        // Visual Studio version found on the Help->About page.
        public string VsVersion { get; set; }
    }
}

// Copyright 2021 Google LLC
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
using System;
using System.Diagnostics;

namespace YetiVSI.DebugEngine
{
    /// <summary>
    /// Class for holding the placeholder module properties that must be transferred
    /// to the real module.
    /// </summary>
    public class PlaceholderModuleProperties
    {
        public readonly long Slide;
        public readonly SbFileSpec PlatformFileSpec;

        public PlaceholderModuleProperties(long slide, SbFileSpec platformFileSpec)
        {
            Slide = slide;
            PlatformFileSpec = platformFileSpec;
        }
    }

    public static class LldbModuleUtil
    {
        /// <summary>Whether the module has symbols loaded.</summary>
        public static bool HasSymbolsLoaded(this SbModule module) => module.HasCompileUnits();

        /// <summary>
        /// Whether the module is a placeholder module. Placeholder modules are created by LLDB
        /// during minidump loading to take the place of modules with missing binaries.
        /// </summary>
        public static bool IsPlaceholderModule(this SbModule module) => 
            module.GetNumSections() == 1
            && module.FindSection(".module_image") != null;

        /// <summary>Whether the module's binary is loaded.</summary>
        public static bool HasBinaryLoaded(this SbModule module) => !IsPlaceholderModule(module);

        /// <summary>
        /// Returns the properties of the supplied placeholder module (file and load address)
        /// or null on rpc errors.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown if |placeholderModule| is not a placeholder module.
        /// </exception>
        public static PlaceholderModuleProperties GetPlaceholderProperties(
            this SbModule placeholderModule,
            RemoteTarget lldbTarget)
        {
            SbSection placeholderSection = placeholderModule.FindSection(".module_image");
            if (placeholderSection == null)
            {
                // Could be either an RPC error or a usage error. Hmm...
                throw new ArgumentException(
                    "Placeholder properties can only be copied from placeholder modules.");
            }

            // The load address of the placeholder section represents the base load address of the
            // module as a whole in the original process.
            ulong placeholderBaseLoadAddress = placeholderSection.GetLoadAddress(lldbTarget);
            if (placeholderBaseLoadAddress == DebuggerConstants.INVALID_ADDRESS)
            {
                Trace.WriteLine("Failed to get load address from the placeholder section.");
                return null;
            }

            // |slide| is how much we need to offset the module's load address by
            long slide = (long)placeholderBaseLoadAddress;

            SbFileSpec fileSpec = placeholderModule.GetPlatformFileSpec();
            if (fileSpec == null)
            {
                Trace.WriteLine("Failed to get file spec from placeholder module.");
                return null;
            }
            return new PlaceholderModuleProperties(slide, fileSpec);
        }

        /// <summary>
        /// Sets |properties|, i.e., file path and load address, on the module |destModule|.
        /// Returns false on rpc errors.
        /// </summary>
        public static bool ApplyPlaceholderProperties(this SbModule destModule,
            PlaceholderModuleProperties properties, RemoteTarget lldbTarget)
        {
            long slide = properties.Slide;
            SbAddress headerAddress = destModule.GetObjectFileHeaderAddress();
            if (headerAddress != null)
            {
                // For libraries this will generally equal 0, for executables it will equal
                // |placeholderBaseLoadAddress|.
                ulong fileBaseAddress = headerAddress.GetFileAddress();
                slide -= (long)fileBaseAddress;
            }

            SbError error = lldbTarget.SetModuleLoadAddress(destModule, slide);
            if (error.Fail())
            {
                Trace.WriteLine(
                    $"Failed to set load address on destination module: {error.GetCString()}.");
                return false;
            }

            if (!destModule.SetPlatformFileSpec(properties.PlatformFileSpec))
            {
                Trace.WriteLine("Failed to set file spec on the destination module.");
                return false;
            }

            return true;
        }
    }
}

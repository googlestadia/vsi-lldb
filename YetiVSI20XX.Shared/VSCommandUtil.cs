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

using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;

namespace YetiVSI
{
    // Collection of helper functions to work with Visual Studio commands.  Used by
    // IOleCommandTargets implementations.
    public class VSCommandUtil
    {
        // Attempts to demarshal |pvaIn| into a string and returns it via |arguments|.
        //
        // Returns true on success.
        public static bool GetArgsAsString(IntPtr pvaIn, out string arguments)
        {
            arguments = null;
            if (pvaIn == IntPtr.Zero)
            {
                // No arguments.
                return false;
            }

            object vaInObject = Marshal.GetObjectForNativeVariant(pvaIn);
            if (vaInObject == null || vaInObject.GetType() != typeof(string))
            {
                return false;
            }

            arguments = vaInObject as string;
            return true;
        }

        // Used to determine if the shell is querying for the parameter list.
        public static bool IsQueryParameterList(System.IntPtr pvaIn, System.IntPtr pvaOut,
            uint nCmdexecopt)
        {
            ushort lo = (ushort)(nCmdexecopt & (uint)0xffff);
            ushort hi = (ushort)(nCmdexecopt >> 16);
            if (lo == (ushort)OLECMDEXECOPT.OLECMDEXECOPT_SHOWHELP)
            {
                if (hi == VsMenus.VSCmdOptQueryParameterList)
                {
                    if (pvaOut != IntPtr.Zero)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}

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

﻿using Microsoft.Win32.SafeHandles;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace YetiVSI
{
    public static class NativeMethods
    {
        enum FileAccessNative : uint
        {
            FILE_READ_EA = 0x00000008,
        }

        /// <summary>
        /// According to
        /// https://docs.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilea
        /// FILE_FLAG_BACKUP_SEMANTICS should be set to obtain a handle to a directory.
        /// </summary>
        enum FileAttributesNative : uint
        {
            FILE_FLAG_BACKUP_SEMANTICS = 0x02000000,
        }

        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern int GetFinalPathNameByHandle(
            SafeFileHandle hFile, [MarshalAs(UnmanagedType.LPTStr)] StringBuilder lpszFilePath,
            int cchFilePath, int dwFlags);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern SafeFileHandle CreateFile(
            [MarshalAs(UnmanagedType.LPTStr)] string filename,
            [MarshalAs(UnmanagedType.U4)] FileAccessNative access,
            [MarshalAs(UnmanagedType.U4)] FileShare share,
            IntPtr securityAttributes, // optional SECURITY_ATTRIBUTES struct or IntPtr.Zero
            [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
            [MarshalAs(UnmanagedType.U4)] FileAttributesNative flagsAndAttributes,
            IntPtr templateFile);

        /// <summary>
        /// Returns target path of the symlink.
        /// </summary>
        public static string GetTargetPathName(string symlink)
        {
            using (SafeFileHandle handle = CreateFile(
                       symlink, FileAccessNative.FILE_READ_EA, FileShare.Read, IntPtr.Zero,
                       FileMode.Open, FileAttributesNative.FILE_FLAG_BACKUP_SEMANTICS,
                       IntPtr.Zero))
            {
                if (handle.IsInvalid)
                {
                    Trace.WriteLine($"Failed to find path `{symlink}`");
                    return "";
                }

                int targetPathSize = GetFinalPathNameByHandle(handle, null, 0, 0);
                if (targetPathSize == 0)
                {
                    Trace.WriteLine($"Failed to get size for `{symlink}` target name");
                    return "";
                }

                // GetFinalPathNameByHandle buffer size doesn't include a null termination
                // character;
                StringBuilder targetPath = new StringBuilder(targetPathSize + 1);
                GetFinalPathNameByHandle(handle, targetPath, targetPathSize, 0);
                return targetPath.ToString();
            }
        }
    }
}

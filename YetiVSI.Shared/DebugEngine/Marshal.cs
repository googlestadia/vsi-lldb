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

using Microsoft.VisualStudio.Debugger.Interop;
using System;

namespace YetiVSI.DebugEngine
{
    public class Marshal
    {
        // Marshal an IDebugDocumentPosition2 from a document position IntPtr.
        public virtual IDebugDocumentPosition2 GetDocumentPositionFromIntPtr(
            IntPtr documentPositionPtr)
        {
            if (documentPositionPtr == IntPtr.Zero)
            {
                return null;
            }

            return (IDebugDocumentPosition2)System.Runtime.InteropServices.Marshal
                .GetObjectForIUnknown(documentPositionPtr);
        }

        // Marshal an IDebugFunctionPosition2 from a function position IntPtr.
        public virtual IDebugFunctionPosition2 GetFunctionPositionFromIntPtr(
            IntPtr functionPositionPtr)
        {
            if (functionPositionPtr == IntPtr.Zero)
            {
                return null;
            }

            return (IDebugFunctionPosition2)System.Runtime.InteropServices.Marshal
                .GetObjectForIUnknown(functionPositionPtr);
        }

        public virtual IDebugCodeContext2 GetCodeContextFromIntPtr(
            IntPtr codeContextPtr)
        {
            if (codeContextPtr == IntPtr.Zero)
            {
                return null;
            }

            return (IDebugCodeContext2)System.Runtime.InteropServices.Marshal
                .GetObjectForIUnknown(codeContextPtr);
        }

        public virtual string GetStringFromIntPtr(IntPtr stringPtr)
        {
            if (stringPtr == IntPtr.Zero)
            {
                return null;
            }
            return System.Runtime.InteropServices.Marshal.PtrToStringBSTR(stringPtr);
        }
    }
}

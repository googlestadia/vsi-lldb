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
    public static class AD7Constants
    {
        // Well known AD7 HRESULTS, as defined in msdbg.idl.
        public const int E_BP_DELETED = unchecked((int)0x80040060);
        public const int E_CRASHDUMP_UNSUPPORTED = unchecked((int)0x80040211);
        public const int E_PORTSUPPLIER_NO_PORT = unchecked((int)0x80040080);
        public const int E_PORT_NO_REQUEST = unchecked((int)0x80040090);
        public const int E_SETVALUE_VALUE_CANNOT_BE_SET = unchecked((int)0x80040521);
        public const int S_GETMEMORYCONTEXT_NO_MEMORY_CONTEXT = unchecked((int)0x40561);

        public const string CppLanguage = "3a12d0b7-c26c-11d0-b442-00a0244a1dd2";
        public const string CLanguage = "63A08714-FC37-11D2-904C-00C04FA302A1";
        public const string CSharpLanguage = "3F5162F8-07C6-11D3-9053-00C04FA302A1";
        static Guid GuidLanguageCpp { get; } = new Guid(CppLanguage);

        static Guid GuidLanguageCs { get; } = new Guid(CSharpLanguage);

        static Guid GuidLanguageC { get; } = new Guid(CLanguage);

        public static string GetLanguageByGuid(Guid guid)
        {
            if (guid == GuidLanguageC)
            {
                return "C";
            }

            if (guid == GuidLanguageCpp)
            {
                return "C++";
            }

            if (guid == GuidLanguageCs)
            {
                return "C#";
            }

            return string.Empty;
        }

        // The state Visual Studio sets when an exception is set to stop.
        public const enum_EXCEPTION_STATE VsExceptionStopState =
            enum_EXCEPTION_STATE.EXCEPTION_STOP_FIRST_CHANCE |
            enum_EXCEPTION_STATE.EXCEPTION_STOP_SECOND_CHANCE |
            enum_EXCEPTION_STATE.EXCEPTION_STOP_USER_FIRST_CHANCE;
        // The state Visual Studio sets when an exception is set to not stop.
        public const enum_EXCEPTION_STATE VsExceptionContinueState =
            enum_EXCEPTION_STATE.EXCEPTION_STOP_SECOND_CHANCE;
        // The state we look at in the debugger to determine if we should stop on an exception or
        // not.
        public const enum_EXCEPTION_STATE ExceptionStopState =
            enum_EXCEPTION_STATE.EXCEPTION_STOP_FIRST_CHANCE;
    }
}

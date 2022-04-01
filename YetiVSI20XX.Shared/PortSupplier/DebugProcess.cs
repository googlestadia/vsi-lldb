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
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;

namespace YetiVSI.PortSupplier
{
    // DebugProcess represents a remote process running on the gamelet.  It contains a mapping to a
    // single 'program'.
    public class DebugProcess : IDebugProcess2
    {
        IDebugPort2 port;
        uint pid;
        string title;
        string command;
        Guid guid;
        IDebugProgram2 program;

        public DebugProcess(IDebugPort2 port, uint pid, string title, string command)
        {
            this.port = port;
            this.pid = pid;
            this.title = title;
            this.command = command;
            this.guid = Guid.NewGuid();
            this.program = new DebugProgram(this);
        }

        public int Attach(IDebugEventCallback2 callback, Guid[] specificEnginesGuid,
            uint numSpecificEngines, int[] engineAttachResults)
        {
            return VSConstants.E_NOTIMPL;
        }

        public int CanDetach()
        {
            return VSConstants.E_NOTIMPL;
        }

        public int CauseBreak()
        {
            return VSConstants.E_NOTIMPL;
        }

        public int Detach()
        {
            return VSConstants.E_NOTIMPL;
        }

        public int EnumPrograms(out IEnumDebugPrograms2 programsEnum)
        {
            programsEnum = new ProgramsEnum(new[] {program});
            return VSConstants.S_OK;
        }

        public int EnumThreads(out IEnumDebugThreads2 threadsEnum)
        {
            threadsEnum = null;
            return VSConstants.E_NOTIMPL;
        }

        // This method must be called on the main thread because it is part of the IDebugProcess2
        // interface. This requirement can be avoided by calling GgpGetAttachedSessionName directly.
        public int GetAttachedSessionName(out string sessionName) =>
            GgpGetAttachedSessionName(out sessionName);

        public int GgpGetAttachedSessionName(out string sessionName)
        {
            sessionName = "";
            return VSConstants.S_OK;
        }

        public int GetInfo(enum_PROCESS_INFO_FIELDS fields, PROCESS_INFO[] processInfo)
        {
            if ((fields & enum_PROCESS_INFO_FIELDS.PIF_FILE_NAME) != 0)
            {
                var result =
                    GgpGetName(enum_GETNAME_TYPE.GN_FILENAME, out processInfo[0].bstrFileName);
                if (result != VSConstants.S_OK) return result;
                processInfo[0].Fields |= enum_PROCESS_INFO_FIELDS.PIF_FILE_NAME;
            }

            if ((fields & enum_PROCESS_INFO_FIELDS.PIF_BASE_NAME) != 0)
            {
                var result =
                    GgpGetName(enum_GETNAME_TYPE.GN_BASENAME, out processInfo[0].bstrBaseName);
                if (result != VSConstants.S_OK) return result;
                processInfo[0].Fields |= enum_PROCESS_INFO_FIELDS.PIF_BASE_NAME;
            }

            if ((fields & enum_PROCESS_INFO_FIELDS.PIF_TITLE) != 0)
            {
                var result =
                    GgpGetName(enum_GETNAME_TYPE.GN_TITLE, out processInfo[0].bstrTitle);
                if (result != VSConstants.S_OK) return result;
                processInfo[0].Fields |= enum_PROCESS_INFO_FIELDS.PIF_TITLE;
            }

            if ((fields & enum_PROCESS_INFO_FIELDS.PIF_ATTACHED_SESSION_NAME) != 0)
            {
                var result =
                    GgpGetAttachedSessionName(out processInfo[0].bstrAttachedSessionName);
                if (result != VSConstants.S_OK) return result;
                processInfo[0].Fields |= enum_PROCESS_INFO_FIELDS.PIF_ATTACHED_SESSION_NAME;
            }

            if ((fields & enum_PROCESS_INFO_FIELDS.PIF_SESSION_ID) != 0)
            {
                processInfo[0].dwSessionId = 0;
                processInfo[0].Fields |= enum_PROCESS_INFO_FIELDS.PIF_SESSION_ID;
            }

            if ((fields & enum_PROCESS_INFO_FIELDS.PIF_FLAGS) != 0)
            {
                processInfo[0].Flags = 0;
                processInfo[0].Fields |= enum_PROCESS_INFO_FIELDS.PIF_FLAGS;
            }

            return VSConstants.S_OK;
        }

        // This method must be called on the main thread because it is part of the IDebugProcess2
        // interface. This requirement can be avoided by calling GgpGetName directly.
        public int GetName(enum_GETNAME_TYPE type, out string name) =>
            GgpGetName(type, out name);

        public int GgpGetName(enum_GETNAME_TYPE type, out string name)
        {
            switch (type)
            {
                case enum_GETNAME_TYPE.GN_FILENAME:
                case enum_GETNAME_TYPE.GN_TITLE:
                    name = this.title;
                    return VSConstants.S_OK;
                case enum_GETNAME_TYPE.GN_BASENAME:
                case enum_GETNAME_TYPE.GN_NAME:
                    name = this.command;
                    return VSConstants.S_OK;
                default:
                    name = null;
                    return VSConstants.E_NOTIMPL;
            }
        }

        public int GetPhysicalProcessId(AD_PROCESS_ID[] processId)
        {
            if (processId.Length == 0)
            {
                return VSConstants.E_INVALIDARG;
            }
            processId[0] = new AD_PROCESS_ID
            {
                ProcessIdType = (uint) enum_AD_PROCESS_ID.AD_PROCESS_ID_SYSTEM,
                dwProcessId = pid
            };
            return VSConstants.S_OK;
        }

        public int GetPort(out IDebugPort2 port)
        {
            port = this.port;
            return VSConstants.S_OK;
        }

        public int GetProcessId(out Guid guid)
        {
            guid = this.guid;
            return VSConstants.S_OK;
        }

        public int GetServer(out IDebugCoreServer2 server)
        {
            server = null;
            return VSConstants.E_NOTIMPL;
        }

        public int Terminate()
        {
            return VSConstants.E_NOTIMPL;
        }
    }
}
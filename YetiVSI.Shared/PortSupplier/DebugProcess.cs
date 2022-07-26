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
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;

namespace YetiVSI.PortSupplier
{
    // Extension of enum_PROCESS_PROPERTY_TYPE
    // Defined in "Visual Studio 2022\VSSDK\VisualStudioIntegration\Common\IDL\msdbg170.idl"
#pragma warning disable IDE1006 // Naming Styles
    public enum enum_PROCESS_PROPERTY_TYPE_170
#pragma warning restore IDE1006 // Naming Styles
    {
        // Return the command line as a VT_BSTR
        PROCESS_PROPERTY_COMMAND_LINE = 1,

        // Return the current directory as a VT_BSTR
        PROCESS_PROPERTY_CURRENT_DIRECTORY = 2,

        // Return the environment variavles as a VT_BSTR.
        // The format is:
        //   NAME1=VALUE1'\0'
        //   NAME2=VALUE2'\0'
        //   ...
        //   '\0'
        PROCESS_PROPERTY_ENVIRONMENT_VARIABLES = 3,

        PROCESS_PROPERTY_PARENT_PID = 4,
        PROCESS_PROPERTY_APP_POOL_NAME = 5,
        PROCESS_PROPERTY_APP_POOL_BINDINGS = 6
    };

    // Defined in "Visual Studio 2022\VSSDK\VisualStudioIntegration\Common\IDL\msdbg.idl"
    [ComImport]
    [Guid("230A0071-62EF-4CAE-AAC0-8988C37024BF")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDebugProcessQueryProperties
    {
        int QueryProperty(enum_PROCESS_PROPERTY_TYPE_170 dwPropType, out object pvarPropValue);
        int QueryProperties(
            uint celt, enum_PROCESS_PROPERTY_TYPE_170[] rgdwPropTypes, out object[] rgtPropValues);
    }

    // DebugProcess represents a remote process running on the gamelet.  It contains a mapping to a
    // single 'program'.
    public class DebugProcess : IDebugProcess2, IDebugProcessQueryProperties
    {
        readonly IDebugPort2 _port;
        readonly uint _pid;
        readonly uint _ppid;
        readonly string _title;
        readonly string _command;
        readonly Guid _guid;
        readonly IDebugProgram2 _program;

        public DebugProcess(IDebugPort2 port, uint pid, uint ppid, string title, string command)
        {
            _port = port;
            _pid = pid;
            _ppid = ppid;
            _title = title;
            _command = command;
            _guid = Guid.NewGuid();
            _program = new DebugProgram(this);
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
            programsEnum = new ProgramsEnum(new[] { _program });
            return VSConstants.S_OK;
        }

        public int EnumThreads(out IEnumDebugThreads2 threadsEnum)
        {
            threadsEnum = null;
            return VSConstants.E_NOTIMPL;
        }

        // This method must be called on the main thread because it is part of the IDebugProcess2
        // interface. This requirement can be avoided by calling GgpGetAttachedSessionName directly.
        public int GetAttachedSessionName(out string sessionName)
        {
            sessionName = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetInfo(enum_PROCESS_INFO_FIELDS fields, PROCESS_INFO[] processInfo)
        {
            if ((fields & enum_PROCESS_INFO_FIELDS.PIF_FILE_NAME) != 0)
            {
                processInfo[0].bstrFileName = GgpGetName(enum_GETNAME_TYPE.GN_FILENAME);
                processInfo[0].Fields |= enum_PROCESS_INFO_FIELDS.PIF_FILE_NAME;
            }

            if ((fields & enum_PROCESS_INFO_FIELDS.PIF_BASE_NAME) != 0)
            {
                processInfo[0].bstrBaseName = GgpGetName(enum_GETNAME_TYPE.GN_BASENAME);
                processInfo[0].Fields |= enum_PROCESS_INFO_FIELDS.PIF_BASE_NAME;
            }

            if ((fields & enum_PROCESS_INFO_FIELDS.PIF_TITLE) != 0)
            {
                processInfo[0].bstrTitle = GgpGetName(enum_GETNAME_TYPE.GN_TITLE);
                processInfo[0].Fields |= enum_PROCESS_INFO_FIELDS.PIF_TITLE;
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
        public int GetName(enum_GETNAME_TYPE type, out string name)
        {
            name = GgpGetName(type);
            return name != null ? VSConstants.S_OK : VSConstants.S_FALSE;
        }

        public string GgpGetName(enum_GETNAME_TYPE type)
        {
#if VS2019
            return GetNameVS2019(type);
#elif VS2022
            return GetNameVS2022(type);
#else
#error "Unsupported version of Visual Studio"
#endif
        }

        public string GetNameVS2019(enum_GETNAME_TYPE type)
        {
            // This is a legacy implementation for Visual Studio 2017 and 2019.
            // Older version of Visual Studio don't have a way to display the full command-line,
            // so we display the command-line instead of the short filename.
            switch (type)
            {
                case enum_GETNAME_TYPE.GN_TITLE:
                    return _title;

                case enum_GETNAME_TYPE.GN_FILENAME:
                case enum_GETNAME_TYPE.GN_BASENAME:
                case enum_GETNAME_TYPE.GN_NAME:
                    return _command;

                case enum_GETNAME_TYPE.GN_MONIKERNAME:
                case enum_GETNAME_TYPE.GN_URL:
                case enum_GETNAME_TYPE.GN_STARTPAGEURL:
                    return null;
            }

            // Unexpected enum variant.
            return null;
        }

        public string GetNameVS2022(enum_GETNAME_TYPE type)
        {
            // This is proper NextGen implementation for Visual Studio 2022.
            // TODO: Extract more precise information about the processes.

            // See https://docs.microsoft.com/en-us/visualstudio/extensibility/debugger/reference/getname-type
            // for the enum definition.
            switch (type)
            {
                // Specifies a friendly name of the document or context.
                case enum_GETNAME_TYPE.GN_NAME:
                    // Just the base file name as a "friendly" name.
                    return Path.GetFileName(GetFilenameFromCommandLine(_command));

                // Specifies the full path of the document or context.
                case enum_GETNAME_TYPE.GN_FILENAME:
                    // We don't have the actual full executable name, so extract if from the
                    // command line arguments.
                    return GetFilenameFromCommandLine(_command);

                // Specifies a base file name instead of a full path of the document or context.
                case enum_GETNAME_TYPE.GN_BASENAME:
                    return Path.GetFileName(GetFilenameFromCommandLine(_command));

                // Specifies a unique name of the document or context in the form of a moniker.
                case enum_GETNAME_TYPE.GN_MONIKERNAME:
                    // Don't know where it's used, not supported.
                    return null;

                // Specifies a URL name of the document or context.
                case enum_GETNAME_TYPE.GN_URL:
                    // Don't know where it's used, not supported.
                    return null;

                // Specifies a title of the document, if one exists.
                case enum_GETNAME_TYPE.GN_TITLE:
                    // This should be the program "title" as defined by the program itself,
                    // e.g. "Google Chrome" or "Half-Life 3: Return of the Jedi".
                    // We don't have a good way to get this title, so just use the binary name.
                    return _title;

                // Gets the starting page URL for processes.
                case enum_GETNAME_TYPE.GN_STARTPAGEURL:
                    // Don't know where it's used, not supported.
                    return null;
            }

            // Unexpected enum variant.
            return null;
        }

        private string GetFilenameFromCommandLine(string commandLine)
        {
            // Strip all leading/trailing whitespace characters, so that we don't have to deal
            // with the whitespaces at the beginning and the ending of the string. There shouldn't
            // be any, but if they are there -- it's likely a mistake and they should be ignored.
            commandLine = commandLine.Trim();

            string filename = null;
            for (int i = 0; i < commandLine.Length; i++)
            {
                if (char.IsWhiteSpace(commandLine[i]))
                {
                    if (i == 0)
                    {
                        Trace.TraceError(
                            $"Leading whitespace in '{commandLine}', did you forget to Trim()?");
                        continue;
                    }

                    if (commandLine[i - 1] == '\\')
                    {
                        // Whitespace is escaped, continue searching.
                        continue;
                    }

                    filename = commandLine.Substring(0, i);
                    break;
                }
            }
            if (filename == null)
            {
                // Didn't find a non-escaped whitespace, the whole commandline is a filename.
                filename = commandLine;
            }

            // For some reason if the filename has a trailing colon (`:`) it's displayed as
            // an empty entry in the process list.
            return filename.TrimEnd(':');
        }

        public int GetPhysicalProcessId(AD_PROCESS_ID[] processId)
        {
            if (processId.Length == 0)
            {
                return VSConstants.E_INVALIDARG;
            }
            processId[0] = new AD_PROCESS_ID
            {
                ProcessIdType = (uint)enum_AD_PROCESS_ID.AD_PROCESS_ID_SYSTEM,
                dwProcessId = _pid
            };
            return VSConstants.S_OK;
        }

        public int GetPort(out IDebugPort2 port)
        {
            port = _port;
            return VSConstants.S_OK;
        }

        public int GetProcessId(out Guid guid)
        {
            guid = _guid;
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

        public int QueryProperty(
            enum_PROCESS_PROPERTY_TYPE_170 dwPropType, out object pvarPropValue)
        {
            pvarPropValue = GetExtendedProperty(dwPropType);
            return VSConstants.S_OK;
        }

        public int QueryProperties(
            uint celt, enum_PROCESS_PROPERTY_TYPE_170[] rgdwPropTypes, out object[] rgtPropValues)
        {
            rgtPropValues = new object[celt];
            for (int i = 0; i < celt; i++)
            {
                rgtPropValues[i] = GetExtendedProperty(rgdwPropTypes[i]);
            }
            return VSConstants.S_OK;
        }

        private object GetExtendedProperty(enum_PROCESS_PROPERTY_TYPE_170 dwPropType)
        {
            object pvarPropValue;
            switch (dwPropType)
            {
                case enum_PROCESS_PROPERTY_TYPE_170.PROCESS_PROPERTY_COMMAND_LINE:
                    pvarPropValue = _command;
                    break;
                case enum_PROCESS_PROPERTY_TYPE_170.PROCESS_PROPERTY_PARENT_PID:
                    pvarPropValue = _ppid;
                    break;
                // Everything else is not supported.
                default:
                    pvarPropValue = null;
                    break;
            }

            return pvarPropValue;
        }
    }
}

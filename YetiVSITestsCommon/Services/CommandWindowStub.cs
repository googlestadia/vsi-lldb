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

﻿using Microsoft.VisualStudio.Shell.Interop;
using System;
using TestsCommon.TestSupport;

namespace YetiVSITestsCommon.Services
{
    public class CommandWindowStub : IVsCommandWindow
    {
        #region IVSCommandWindow

        public int Create()
        {
            throw new NotImplementedTestDoubleException();
        }

        public int EchoCommand(string szCommand)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int ExecuteCommand(string szCommand)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int LogToFile(string szLogFile, uint grfFlags)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int PrepareCommand(string szCommand, out Guid pguidCmdGroup, out uint pdwCmdID,
            out IntPtr ppvaCmdArg, PREPARECOMMANDRESULT[] pResult)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int Print(string szTextToPrint)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int PrintNoShow(string szTextToPrint)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int RunningCommandWindowCommand(out int pfCmdWin)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int SetCurrentLanguageService(ref Guid rguidLanguageService)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int SetMode(COMMANDWINDOWMODE mode)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int Show()
        {
            throw new NotImplementedTestDoubleException();
        }

        public int StopLogging()
        {
            throw new NotImplementedTestDoubleException();
        }

        #endregion
    }
}

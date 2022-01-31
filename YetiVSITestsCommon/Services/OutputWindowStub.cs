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

﻿using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using TestsCommon.TestSupport;

namespace YetiVSITestsCommon.Services
{
    public class OutputWindowStub : IVsOutputWindow, SVsOutputWindow
    {
        readonly IDictionary<Guid, IVsOutputWindowPane> panes =
            new Dictionary<Guid, IVsOutputWindowPane>();

        public int GetPane(ref Guid rguidPane, out IVsOutputWindowPane ppPane)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!panes.ContainsKey(rguidPane))
            {
                int result = CreatePane(ref rguidPane, "", 0, 0);
                if (result != VSConstants.S_OK)
                {
                    ppPane = null;
                    return result;
                }
            }
            ppPane = panes[rguidPane];
            return VSConstants.S_OK;
        }

        public int CreatePane(ref Guid rguidPane, string pszPaneName, int fInitVisible,
            int fClearWithSolution)
        {
            panes.Add(rguidPane, new OutputWindowPaneStub());
            return VSConstants.S_OK;
        }

        public int DeletePane(ref Guid rguidPane)
        {
            panes.Remove(rguidPane);
            return VSConstants.S_OK;
        }
    }

    public class OutputWindowPaneStub : IVsOutputWindowPane
    {
        public List<string> OutputLines { get; } = new List<string>();

        public int OutputString(string pszOutputString)
        {
            OutputLines.Add(pszOutputString);
            return VSConstants.S_OK;
        }

        #region Not Implemented

        public int Activate()
        {
            throw new NotImplementedTestDoubleException();
        }

        public int Hide()
        {
            throw new NotImplementedTestDoubleException();
        }

        public int Clear()
        {
            throw new NotImplementedTestDoubleException();
        }

        public int FlushToTaskList()
        {
            throw new NotImplementedTestDoubleException();
        }

        public int OutputTaskItemString(string pszOutputString, VSTASKPRIORITY nPriority,
            VSTASKCATEGORY nCategory, string pszSubcategory, int nBitmap, string pszFilename,
            uint nLineNum, string pszTaskItemText)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int OutputTaskItemStringEx(string pszOutputString, VSTASKPRIORITY nPriority,
            VSTASKCATEGORY nCategory, string pszSubcategory, int nBitmap, string pszFilename,
            uint nLineNum, string pszTaskItemText, string pszLookupKwd)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int GetName(ref string pbstrPaneName)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int SetName(string pszPaneName)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int OutputStringThreadSafe(string pszOutputString)
        {
            throw new NotImplementedTestDoubleException();
        }

        #endregion
    }
}

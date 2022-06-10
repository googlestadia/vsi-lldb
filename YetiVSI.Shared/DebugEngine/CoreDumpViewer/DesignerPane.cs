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
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Runtime.InteropServices;

namespace YetiVSI.DebugEngine.CoreDumpViewer
{
    [ComVisible(true)]
    class DesignerPane : WindowPane, IVsPersistDocData
    {
        readonly string _filename;
        internal DesignerPane(IServiceProvider serviceProvider, string filename)
            : base(serviceProvider)
        {
            _filename = filename;
        }

        protected override void Initialize()
        {
            base.Initialize();
            // Create window and pass all needed information for it.
            Content = new CoreDumpViewerWindow(_filename);
        }

        public int GetGuidEditorType(out Guid pClassID)
        {
            pClassID = CoreDumpViewerFactory.GuidCoreDumpViewer;
            return VSConstants.S_OK;
        }

        public int IsDocDataDirty(out int pfDirty)
        {
            pfDirty = 0;
            return VSConstants.S_OK;
        }

        public int SetUntitledDocPath(string pszDocDataPath) => VSConstants.E_NOTIMPL;

        public int LoadDocData(string pszMkDocument) => VSConstants.S_OK;

        public int SaveDocData(VSSAVEFLAGS dwSave, out string pbstrMkDocumentNew,
                               out int pfSaveCanceled)
        {
            pbstrMkDocumentNew = "";
            pfSaveCanceled = 0;
            return VSConstants.E_NOTIMPL;
        }

        public int Close() => VSConstants.S_OK;

        public int OnRegisterDocData(uint docCookie, IVsHierarchy pHierNew,
                                     uint itemidNew) => VSConstants.S_OK;

        public int RenameDocData(uint grfAttribs, IVsHierarchy pHierNew, uint itemidNew,
                                 string pszMkDocumentNew) => VSConstants.E_NOTIMPL;

        public int IsDocDataReloadable(out int pfReloadable)
        {
            pfReloadable = 0;
            return VSConstants.S_OK;
        }

        public int ReloadDocData(uint grfFlags) => VSConstants.E_NOTIMPL;
    }
}

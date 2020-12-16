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
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace YetiVSI.DebugEngine.CoreDumpViewer
{
    [Guid(GuidCoreDumpViewerString)]
    public class CoreDumpViewerFactory : IVsEditorFactory
    {
        ServiceProvider _serviceProvider;
        IOleServiceProvider _site;
        AsyncPackage _package;

        public const string GuidCoreDumpViewerString = "5a27d995-f7c5-4705-84f1-a929f4685bff";
        public static Guid GuidCoreDumpViewer = new Guid(GuidCoreDumpViewerString);

        public CoreDumpViewerFactory(AsyncPackage package)
        {
            _package = package;
        }

        public int CreateEditorInstance(uint grfCreateDoc, string pszMkDocument,
                                        string pszPhysicalView, IVsHierarchy pvHier, uint itemid,
                                        IntPtr punkDocDataExisting, out IntPtr ppunkDocView,
                                        out IntPtr ppunkDocData, out string pbstrEditorCaption,
                                        out Guid pguidCmdUI, out int pgrfCDW)
        {
            // Initialize to null
            ppunkDocView = IntPtr.Zero;
            ppunkDocData = IntPtr.Zero;
            pguidCmdUI = GuidCoreDumpViewer;
            pgrfCDW = 0;
            pbstrEditorCaption = null;

            // Validate inputs
            if ((grfCreateDoc & (VSConstants.CEF_OPENFILE | VSConstants.CEF_SILENT)) == 0)
            {
                return VSConstants.E_INVALIDARG;
            }
            if (punkDocDataExisting != IntPtr.Zero)
            {
                return VSConstants.VS_E_INCOMPATIBLEDOCDATA;
            }

            // Create the Document view (this instance should implement IVsPersistDocData,
            // IOleCommandTarget and IVsWindowPane) and can be used both for docView and
            // docData variables.
            var newEditor = new DesignerPane(_serviceProvider, pszMkDocument);
            ppunkDocView = System.Runtime.InteropServices.Marshal.GetIUnknownForObject(newEditor);
            ppunkDocData = System.Runtime.InteropServices.Marshal.GetIUnknownForObject(newEditor);
            pbstrEditorCaption = "";

            return VSConstants.S_OK;
        }

        public int SetSite(IOleServiceProvider psp)
        {
            _site = psp;
            _serviceProvider = new ServiceProvider(psp);
            return VSConstants.S_OK;
        }

        public int Close() => VSConstants.S_OK;

        public int MapLogicalView(ref Guid logicalView, out string physicalView)
        {
            physicalView = null;
            // we support only a single physical view
            return VSConstants.LOGVIEWID_Primary == logicalView ? VSConstants.S_OK
                                                                : VSConstants.E_NOTIMPL;
        }
    }
}
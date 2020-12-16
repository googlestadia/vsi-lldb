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

﻿using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace YetiVSI.DebugEngine.CoreDumpViewer
{
    /// <summary>
    /// This package is loaded on the first load of a *.core file in Visual Studio.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(_packageGuidString)]
    [ProvideEditorFactory(typeof(CoreDumpViewerFactory), 110)]
    [ProvideEditorExtension(typeof(CoreDumpViewerFactory), ".core", 50)]
    [InstalledProductRegistration("CoreDumpViewer", "Core Dump viewer", "1.1.0")]
    public sealed class CoreDumpViewerPackage : AsyncPackage
    {
        const string _packageGuidString = "746ec37a-e0d3-4b69-a254-09554d3db2a7";
        protected override async Task InitializeAsync(CancellationToken cancellationToken,
                                                      IProgress<ServiceProgressData> progress)
        {
            var editorFactory = new CoreDumpViewerFactory(this);
            RegisterEditorFactory(editorFactory);

            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        }
    }
}

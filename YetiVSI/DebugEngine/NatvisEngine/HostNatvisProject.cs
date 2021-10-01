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
using System.Linq;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio;
using System.Collections.Generic;
using Microsoft.VisualStudio.Threading;
using YetiVSI.Util;
using System.Diagnostics;

namespace YetiVSI.DebugEngine.NatvisEngine
{
    /// <summary>
    /// Provides interactions with the host's source workspace to locate and load any natvis files
    /// in the project.
    /// </summary>
    ///
    /// TODO: Support files in root Solution.
    public class HostNatvisProject : INatvisFileSource
    {
        private JoinableTaskContext taskContext;

        public HostNatvisProject(JoinableTaskContext taskContext)
        {
            this.taskContext = taskContext;
        }

        #region INatvisFileSource

        public IEnumerable<string> GetFilePaths()
        {
            List<string> paths = new List<string>();
            try
            {
                taskContext.Factory.Run(async () =>
                {
                    await taskContext.Factory.SwitchToMainThreadAsync();
                    FindNatvisInSolutionImpl(paths);
                });
            }
            catch (Exception e)
            {
                Trace.WriteLine("WARNING: Failed to find Natvis files in solution. Exception: " +
                    e.ToString());
            }
            return paths;
        }

        #endregion

        // from vsshell.idl
        private enum VSENUMPROJFLAGS
        {
            // normal projects referenced in the solution file and currently loaded
            EPF_LOADEDINSOLUTION = 0x00000001,
            // normal projects referenced in the solution file and currently NOT loaded
            EPF_UNLOADEDINSOLUTION = 0x00000002,
            //  all normal projects referenced in the solution file
            EPF_ALLINSOLUTION = (EPF_LOADEDINSOLUTION | EPF_UNLOADEDINSOLUTION),
            // projects with project type GUID matching parameter
            EPF_MATCHTYPE = 0x00000004,
            // 'virtual' projects visible as top-level projects in Solution Explorer
            //  (NOTE: these are projects that are not directly referenced in the solution file;
            //  instead they are projects that are created programmatically via a non-standard
            // UI.)
            EPF_VIRTUALVISIBLEPROJECT = 0x00000008,
            //   'virtual' projects NOT visible as top-level projects in Solution Explorer
            //  (NOTE: these are projects that are not directly referenced in the solution file
            //  and are usually displayed as nested (a.k.a. sub) projects in Solution Explorer)
            EPF_VIRTUALNONVISIBLEPROJECT = 0x00000010,
            //  all 'virtual' projects of any kind
            EPF_ALLVIRTUAL = (EPF_VIRTUALVISIBLEPROJECT | EPF_VIRTUALNONVISIBLEPROJECT),
            //  all projects including normal projects directly referenced in the solution
            //  file as well as all virtual projects including nested (a.k.a. sub) projects
            EPF_ALLPROJECTS = (EPF_ALLINSOLUTION | EPF_ALLVIRTUAL),
        };

        private enum LoadingMethod
        {
            // Matches ".natvis" files under VS project.
            ByExtension,
            // Matches files with internal property "C++ debugger visualization file".
            ByItemType,
        }

        private void FindNatvisInSolutionImpl(List<string> paths)
        {
            taskContext.ThrowIfNotOnMainThread();

            var solution = (IVsSolution)Package.GetGlobalService(typeof(SVsSolution));
            if (solution == null)
            {
                // failed to find a solution
                return;
            }

            IEnumHierarchies enumProjects;
            int hr = solution.GetProjectEnum((uint)VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION |
                                                (uint)VSENUMPROJFLAGS.EPF_MATCHTYPE,
                                                new Guid("{8bc9ceb8-8b4a-11d0-8d11-00a0c91bc942}"),
                                                out enumProjects);
            if (hr != VSConstants.S_OK)
            {
                return;
            }

            IVsHierarchy[] proj = new IVsHierarchy[1];
            uint count;
            while (VSConstants.S_OK == enumProjects.Next(1, proj, out count))
            {
                List<string> projectPaths = new List<string>();
                // Load files by ".natvis" extension.
                LoadNatvisFromProject(proj[0], projectPaths, LoadingMethod.ByExtension);
                // Load files tagged as "C++ debugger visualization file" in VS project.
                LoadNatvisFromProject(proj[0], projectPaths, LoadingMethod.ByItemType);
                // Filter out duplicates in the case the same file was loaded twice.
                paths.AddRange(projectPaths.Distinct().ToList());
            }

            // Also, look for natvis files in top-level solution items
            hr = solution.GetProjectEnum((uint)VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION |
                                            (uint)VSENUMPROJFLAGS.EPF_MATCHTYPE,
                                            new Guid("{2150E333-8FDC-42A3-9474-1A3956D46DE8}"),
                                            out enumProjects);
            if (hr != VSConstants.S_OK)
            {
                return;
            }

            while (VSConstants.S_OK == enumProjects.Next(1, proj, out count))
            {
                // On a solution level there is no information about file type (e.g. "C++ debugger
                // visualization file"). Only load files by extension.
                LoadNatvisFromProject(proj[0], paths, LoadingMethod.ByExtension);
            }
        }

        private void LoadNatvisFromProject(IVsHierarchy hier, List<string> paths,
                                           LoadingMethod loadingMethod)
        {
            taskContext.ThrowIfNotOnMainThread();

            IVsProject4 proj = hier as IVsProject4;
            if (proj == null)
            {
                return;
            }

            // Retrieve up to 10 natvis files in the first pass.  This avoids the need to
            // iterate through the file list a second time if we don't have more than 10
            // solution -level natvis files.
            uint cActual;
            uint[] itemIds = new uint[10];

            if (!GetNatvisFiles(loadingMethod, proj, 10, itemIds, out cActual))
            {
                return;
            }

            // If the pre-allocated buffer of 10 natvis files was not enough, reallocate the
            // buffer and repeat.
            if (cActual > 10)
            {
                itemIds = new uint[cActual];
                if (!GetNatvisFiles(loadingMethod, proj, cActual, itemIds, out cActual))
                {
                    return;
                }
            }

            // Now, obtain the full path to each of our natvis files and return it.
            for (uint i = 0; i < cActual; i++)
            {
                string document;
                if (VSConstants.S_OK == proj.GetMkDocument(itemIds[i], out document))
                {
                    paths.Add(document);
                }
            }
        }

        private bool GetNatvisFiles(LoadingMethod loadingMethod, IVsProject4 proj, uint celt,
                                    uint[] rgitemids, out uint cActual)
        {
            taskContext.ThrowIfNotOnMainThread();

            int status = loadingMethod == LoadingMethod.ByExtension
                             ? proj.GetFilesEndingWith(".natvis", celt, rgitemids, out cActual)
                             : proj.GetFilesWithItemType("natvis", celt, rgitemids, out cActual);

            if (status != VSConstants.S_OK)
            {
                Trace.WriteLine($"WARNING: Failed to load Natvis files in solution. " +
                                $"Method: {loadingMethod}, error code: {status}");
                return false;
            }

            return true;
        }
    }
}

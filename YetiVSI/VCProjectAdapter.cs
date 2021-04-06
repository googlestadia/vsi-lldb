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

using Microsoft.VisualStudio.VCProjectEngine;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using YetiCommon;
using YetiVSI.ProjectSystem.Abstractions;

namespace YetiVSI
{
    // Project properties of C++ projects.
    public class VcProjectAdapter: ISolutionExplorerProject
    {
        public class Factory
        {
            public virtual ISolutionExplorerProject Create(VCProject vcProject)
            {
                try
                {
                    return new VcProjectAdapter(vcProject);
                }
                catch (COMException ex)
                {
                    Trace.WriteLine($"Warning: Failed to create VcProjectAdapter for project " +
                        $"{vcProject.Name} of type {vcProject.Kind}.{Environment.NewLine}{ex}");
                    return null;
                }
            }
        }

        readonly VCProject vcProject;
        readonly IVCRulePropertyStorage generalRule;
        readonly IVCRulePropertyStorage nmakeRule;

        VcProjectAdapter(VCProject vcProject)
        {
            vcProject.LoadUserFile();
            this.vcProject = vcProject;
            var vcConfiguration = vcProject.ActiveConfiguration;
            generalRule =
                vcConfiguration.Rules.Item("ConfigurationGeneral") as IVCRulePropertyStorage;
            nmakeRule = vcConfiguration.Rules.Item(
                "ConfigurationNMake") as IVCRulePropertyStorage;
        }

        public string OutputDirectory =>
            FileUtil.RemoveTrailingSeparator(GetOutputDirectoryNotTrimmed());

        public string TargetDirectory
        {
            get
            {
                var targetPath = GetTargetPathImpl();
                return string.IsNullOrEmpty(targetPath) ? "" : Path.GetDirectoryName(targetPath);
            }
        }

        string GetOutputDirectoryNotTrimmed()
        {
            var outputPath = generalRule.GetEvaluatedPropertyValue(
                ProjectPropertyName.OutputDirectory);
            // Support project relative path: if the output directory is not rooted, treat it as a
            // path under project directory.
            if (!Path.IsPathRooted(outputPath))
            {
                return FileUtil.GetFullPath(outputPath, GetAbsoluteRootPathNotTrimmed());
            }
            return outputPath;
        }

        string GetAbsoluteRootPathNotTrimmed()
        {
            return vcProject.ProjectDirectory;
        }

        string GetTargetPathImpl()
        {
            // TODO: Refactor to remove conditonal logic based on project type.
            // Unknown configuration type maps to Makefile projects.
            if (vcProject.ActiveConfiguration.ConfigurationType == ConfigurationTypes.typeUnknown)
            {
                // Get the target path. If it's relative, use the project directory as the base
                // root. This is the same behaviour as CPS.
                return GetTargetPathImpl(GetAbsoluteRootPathNotTrimmed());
            }
            // Get the target path. If it's relative, use the output directory as the base root.
            // This is the same behaviour as CPS.
            return GetTargetPathImpl(GetOutputDirectoryNotTrimmed());
        }

        /// <summary>
        /// Return the absolute path to the target.
        /// </summary>
        /// <param name="baseRoot">The root to use if the project property is a relative path.
        /// </param>
        string GetTargetPathImpl(string baseRoot)
        {
            var targetPath = generalRule.GetEvaluatedPropertyValue(ProjectPropertyName.TargetPath);
            if (string.IsNullOrEmpty(targetPath))
            {
                // If the targetPath is null, just use the filename (which in some cases can still
                // be a relative path).
                targetPath = GetTargetFileNameProperty();
            }
            if (Path.IsPathRooted(baseRoot))
            {
                return FileUtil.GetFullPath(targetPath, baseRoot);
            }
            else if (Path.IsPathRooted(targetPath))
            {
                // BaseRoot is not rooted so we can't use FileUtil.GetFullPath, but since targetPath
                // is already rooted, we can just use Path.GetFullPath to safely fully resolve the
                // path.
                return Path.GetFullPath(targetPath);
            }
            return "";
        }

        /// <summary>
        /// Returns the target file name. This can be a relative path.
        /// </summary>
        string GetTargetFileNameProperty()
        {
            // TODO: Refactor to remove conditonal logic based on project type.
            // Unknown configuration type maps to Makefile projects.
            string fileName;
            if (vcProject.ActiveConfiguration.ConfigurationType == ConfigurationTypes.typeUnknown)
            {
                fileName = nmakeRule.GetEvaluatedPropertyValue(ProjectPropertyName.NMakeOutput);
            }
            else
            {
                fileName = generalRule.GetEvaluatedPropertyValue(
                    ProjectPropertyName.TargetFileName);
            }
            if (fileName == null)
            {
                fileName = "";
            }
            return fileName;
        }
    }
}

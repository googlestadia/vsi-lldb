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
    public abstract class VsProjectInfo : ISolutionExplorerProject
    {
        public class Factory
        {
            public virtual ISolutionExplorerProject Create(VCProject vcProject)
            {
                try
                {
                    vcProject.LoadUserFile();
                    var activeConfiguration = vcProject.ActiveConfiguration;
                    var generalRule =
                        activeConfiguration.Rules.Item("ConfigurationGeneral")
                            as IVCRulePropertyStorage;

                    var projectDirectory = vcProject.ProjectDirectory;
                    var outputDirectoryProperty =
                        generalRule?.GetEvaluatedPropertyValue(
                            ProjectPropertyName.OutputDirectory) ?? "";
                    var targetPathProperty =
                        generalRule?.GetEvaluatedPropertyValue(
                            ProjectPropertyName.TargetPath) ?? "";

                    // Unknown configuration type maps to Makefile projects.
                    if (activeConfiguration.ConfigurationType == ConfigurationTypes.typeUnknown)
                    {
                        var nmakeRule = activeConfiguration.Rules.Item(
                            "ConfigurationNMake") as IVCRulePropertyStorage;
                        var nmakeOutputProperty =
                            nmakeRule?.GetEvaluatedPropertyValue(
                                ProjectPropertyName.NMakeOutput) ?? "";
                        return new NMakeProjectInfo(
                            projectDirectory, outputDirectoryProperty, targetPathProperty,
                            nmakeOutputProperty);
                    }

                    var targetFileNameProperty =
                        generalRule?.GetEvaluatedPropertyValue(
                            ProjectPropertyName.TargetFileName) ?? "";
                    return new StudioProjectInfo(projectDirectory, outputDirectoryProperty,
                                                    targetPathProperty, targetFileNameProperty);
                }
                catch (Exception ex) when (ex is Microsoft.VisualStudio.ProjectSystem
                    .ProjectException || ex is COMException)
                {
                    Trace.WriteLine(
                        $"Warning: Failed to create VcProjectAdapter for project " +
                        $"{vcProject.Name} of type {vcProject.Kind}. {ex.Demystify()}");
                    return null;
                }
            }
        }

        protected readonly string _projectDirectory;
        readonly string _outputDirectoryProperty;
        readonly string _targetPathProperty;
        readonly string _targetFileNameProperty;

        protected VsProjectInfo(
            string projectDirectory, string outputDirectoryProperty, string targetPathProperty,
            string targetFileNameProperty)
        {
            _projectDirectory = projectDirectory;
            _outputDirectoryProperty = outputDirectoryProperty;
            _targetPathProperty = targetPathProperty;
            _targetFileNameProperty = targetFileNameProperty;
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
            // Support project relative path: if the output directory is not rooted, treat it as a
            // path under project directory.
            if (!Path.IsPathRooted(_outputDirectoryProperty))
            {
                return FileUtil.GetFullPath(_outputDirectoryProperty, _projectDirectory);
            }
            return _outputDirectoryProperty;
        }

        /// <summary>
        /// Return the absolute path to the target.
        /// </summary>
        protected string GetTargetPathImpl()
        {
            var targetPath = _targetPathProperty;
            if (string.IsNullOrEmpty(targetPath))
            {
                // If the targetPath is null, just use the filename (which in some cases can still
                // be a relative path).
                targetPath = _targetFileNameProperty ?? "";
            }
            if (Path.IsPathRooted(BaseRoot))
            {
                return FileUtil.GetFullPath(targetPath, BaseRoot);
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
        /// Return the root to use if the project property is a relative path.
        /// </summary>
        protected abstract string BaseRoot { get; }

        internal sealed class StudioProjectInfo : VsProjectInfo
        {
            internal StudioProjectInfo(
                string projectDirectory, string outputDirectoryProperty, string targetPathProperty,
                string targetFileNameProperty)
                : base(projectDirectory, outputDirectoryProperty, targetPathProperty,
                      targetFileNameProperty)
            {
            }

            protected override string BaseRoot => GetOutputDirectoryNotTrimmed();
        }

        internal sealed class NMakeProjectInfo : VsProjectInfo
        {
            internal NMakeProjectInfo(
                string projectDirectory, string outputDirectoryProperty,
                string targetPathProperty, string nmakeOutputProperty)
                : base(projectDirectory, outputDirectoryProperty, targetPathProperty,
                      nmakeOutputProperty)
            {
            }

            protected override string BaseRoot => _projectDirectory;
        }
    }
}

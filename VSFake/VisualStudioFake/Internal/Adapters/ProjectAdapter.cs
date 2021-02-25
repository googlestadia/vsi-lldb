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

using GgpGrpc.Models;
using Google.VisualStudioFake.API;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using YetiCommon;
using YetiCommon.VSProject;

namespace Google.VisualStudioFake.Internal
{
    public class ProjectAdapter : IProjectAdapter, IAsyncProject, ISolutionExplorerProject
    {
        readonly NLog.ILogger _logger;
        readonly string _samplesDir;

        Project _project;
        string _projectPath;

        public ProjectAdapter(NLog.ILogger logger, string samplesDir)
        {
            _logger = logger;
            _samplesDir = samplesDir;
        }

        public event ProjectLoadedHandler ProjectLoaded;

        public void Load(string sampleName)
        {
            if (_project != null)
            {
                throw new InvalidOperationException("Another project has already been loaded. " +
                                                    "This method should be used only once.");
            }

            string solutionDir = Path.Combine(_samplesDir, sampleName);
            _projectPath = Path.Combine(solutionDir, $"{sampleName}.vcxproj");
            try
            {
                // We use the following overload of `new Project` to guarantee it doesn't
                // use the default project collection, otherwise we can run into issues when trying
                // to load the same sample project from different tests.
                var globalProperties = new Dictionary<string, string>
                {
                    { "Configuration", "Debug" },
                    { "Platform", "GGP" }
                };
                _project = new Project(_projectPath, globalProperties, null,
                                       new ProjectCollection());
                ProjectLoaded?.Invoke(this);
            }
            catch (InvalidProjectFileException e)
            {
                throw new ProjectLoadingException(
                    $"Project with path {_projectPath} could not be loaded.", e);
            }
        }

        public void Build()
        {
            CheckProjectLoaded();

            BuildManager manager = BuildManager.DefaultBuildManager;

            var buildLogger = new StringLogger();
            var projectInstance = new ProjectInstance(_projectPath);
            var buildParams = new BuildParameters()
            {
                DetailedSummary = true,
                Loggers = new List<ILogger> {buildLogger},
            };

            BuildResult result = manager.Build(buildParams,
                                               new BuildRequestData(
                                                   projectInstance, new string[] {"Build"}));

            _logger.Debug("Build output:\n" + buildLogger.GetLog());

            if (result.OverallResult == BuildResultCode.Failure)
            {
                throw new ProjectBuildException(
                    $"Failed to build project {_projectPath}:\n\n{buildLogger.GetLog()}");
            }
        }

        public IAsyncProject Project
        {
            get
            {
                CheckProjectLoaded();
                return this;
            }
        }

        #region ISolutionExplorerProject

        public string OutputDirectory
        {
            get
            {
                var outputDir = _project.GetPropertyValue(ProjectPropertyName.OutputDirectory);
                return string.IsNullOrEmpty(outputDir)
                    ? ""
                    : FileUtil.RemoveTrailingSeparator(Path.GetFullPath(outputDir));
            }
        }

        public string TargetDirectory
        {
            get
            {
                var targetPath = _project.GetPropertyValue(ProjectPropertyName.TargetPath);
                return string.IsNullOrEmpty(targetPath)
                    ? ""
                    : Path.GetDirectoryName(Path.GetFullPath(targetPath));
            }
        }

        #endregion

        #region IAsyncProject

        public Task<string> GetAbsoluteRootPathAsync() => Task.FromResult(_project.DirectoryPath);

        public Task<string> GetOutputDirectoryAsync() => Task.FromResult(OutputDirectory);

        public Task<string> GetTargetDirectoryAsync() => Task.FromResult(TargetDirectory);

        public Task<string> GetTargetPathAsync()
        {
            var targetPath = _project.GetPropertyValue(ProjectPropertyName.TargetPath);
            return Task.FromResult(string.IsNullOrEmpty(targetPath)
                                       ? ""
                                       : Path.GetFullPath(targetPath));
        }

        public Task<string> GetTargetFileNameAsync()
        {
            var targetPath = _project.GetPropertyValue(ProjectPropertyName.TargetPath);
            return Task.FromResult(string.IsNullOrEmpty(targetPath)
                                       ? ""
                                       : Path.GetFileName(targetPath));
        }

        public Task<DeployOnLaunchSetting> GetDeployOnLaunchAsync()
        {
            var deployOnLaunchString =
                _project.GetPropertyValue(ProjectPropertyName.GgpDeployOnLaunch);
            if (!Enum.TryParse(deployOnLaunchString, true,
                               out DeployOnLaunchSetting deployOnLaunch))
            {
                deployOnLaunch = DeployOnLaunchSetting.TRUE;
            }

            return Task.FromResult(deployOnLaunch);
        }

        public void SetDeployOnLaunch(DeployOnLaunchSetting deployOnLaunch) =>
            SetStringProperty(ProjectPropertyName.GgpDeployOnLaunch, deployOnLaunch.ToString());

        public Task<DeployCompressionSetting> GetDeployCompressionAsync()
        {
            string deployCompressionString =
                _project.GetPropertyValue(ProjectPropertyName.GgpDeployCompression);
            DeployCompressionSetting deployCompression;
            if (!Enum.TryParse(deployCompressionString, true, out deployCompression))
            {
                deployCompression = DeployCompressionSetting.Compressed;
            }
            return Task.FromResult(deployCompression);
        }

        public Task<SurfaceEnforcementSetting> GetSurfaceEnforcementAsync()
        {
            string surfaceEnforcementString =
                _project.GetPropertyValue(ProjectPropertyName.GgpSurfaceEnforcementMode);

            if (!Enum.TryParse(surfaceEnforcementString, true,
                               out SurfaceEnforcementSetting surfaceEnforcement))
            {
                surfaceEnforcement = SurfaceEnforcementSetting.Off;
            }

            return Task.FromResult(surfaceEnforcement);
        }

        public void SetSurfaceEnforcement(SurfaceEnforcementSetting setting) =>
            SetStringProperty(ProjectPropertyName.GgpSurfaceEnforcementMode, setting.ToString());

        public Task<bool> GetLaunchRenderDocAsync() =>
            GetBoolPropertyAsync(ProjectPropertyName.GgpLaunchRenderDoc);

        public void SetLaunchRenderDoc(bool launchRenderDoc) =>
            SetBoolProperty(ProjectPropertyName.GgpLaunchRenderDoc, launchRenderDoc);

        public Task<bool> GetLaunchRgpAsync() =>
            GetBoolPropertyAsync(ProjectPropertyName.GgpLaunchRgp);

        public void SetLaunchRgp(bool launchRgp) =>
            SetBoolProperty(ProjectPropertyName.GgpLaunchRgp, launchRgp);

        public Task<string> GetExecutablePathAsync() =>
            GetStringPropertyAsync(ProjectPropertyName.ExecutablePath);

        public Task<string> GetApplicationAsync() =>
            GetStringPropertyAsync(ProjectPropertyName.GgpApplication);

        public Task<string> GetCustomDeployOnLaunchAsync() =>
            GetStringPropertyAsync(ProjectPropertyName.GgpCustomDeployOnLaunch);

        public Task<string> GetGameletEnvironmentVariablesAsync() =>
            GetStringPropertyAsync(ProjectPropertyName.GgpGameletEnvironmentVariables);

        public void SetGameletEnvironmentVariables(string envVars) =>
            SetStringProperty(ProjectPropertyName.GgpGameletEnvironmentVariables, envVars);

        public Task<string> GetGameletLaunchArgumentsAsync() =>
            GetStringPropertyAsync(ProjectPropertyName.GgpGameletLaunchArguments);

        public Task<string> GetVulkanDriverVariantAsync() =>
            GetStringPropertyAsync(ProjectPropertyName.GgpVulkanDriverVariant);

        public void SetVulkanDriverVariant(string vulkanDriverVariant) =>
            SetStringProperty(ProjectPropertyName.GgpVulkanDriverVariant, vulkanDriverVariant);

        public Task<string> GetTestAccountAsync() =>
            GetStringPropertyAsync(ProjectPropertyName.GgpTestAccount);

        public void SetTestAccount(string testAccount) =>
            SetStringProperty(ProjectPropertyName.GgpTestAccount, testAccount);

        public Task<string> GetQueryParamsAsync() =>
            GetStringPropertyAsync(ProjectPropertyName.GgpQueryParams);

        public void SetQueryParams(string queryParams) =>
            SetStringProperty(ProjectPropertyName.GgpQueryParams, queryParams);

        #endregion

        Task<string> GetStringPropertyAsync(string propertyName) =>
            Task.FromResult(_project.GetPropertyValue(propertyName));

        void SetStringProperty(string propertyName, string value) =>
            _project.SetProperty(propertyName, value);

        Task<bool> GetBoolPropertyAsync(string propertyName)
        {
            var propertyString = _project.GetPropertyValue(propertyName);
            return Task.FromResult(!string.IsNullOrEmpty(propertyString) &&
                                   bool.Parse(propertyString));
        }

        void SetBoolProperty(string propertyName, bool value) =>
            _project.SetProperty(propertyName, value.ToString());

        void CheckProjectLoaded()
        {
            if (_project == null)
            {
                throw new InvalidOperationException("No project has been loaded. Please load one " +
                                                    $"using {nameof(ProjectAdapter.Load)} " +
                                                    "before calling this method.");
            }
        }

        class StringLogger : ILogger
        {
            readonly StringBuilder _log = new StringBuilder();
            int _indent;

            readonly Dictionary<MessageImportance, LoggerVerbosity> _importanceToVerbosity =
                new Dictionary<MessageImportance, LoggerVerbosity>()
                {
                    {MessageImportance.High, LoggerVerbosity.Minimal},
                    {MessageImportance.Normal, LoggerVerbosity.Normal},
                    {MessageImportance.Low, LoggerVerbosity.Detailed},
                };

            public string GetLog() => _log.ToString();

            #region ILogger

            public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Minimal;

            public string Parameters { get; set; }

            public void Initialize(IEventSource eventSource)
            {
                eventSource.ErrorRaised += OnErrorRaised;
                eventSource.WarningRaised += OnWarningRaised;
                eventSource.MessageRaised += OnMessageRaised;
                eventSource.ProjectStarted += OnProjectStarted;
                eventSource.ProjectFinished += OnProjectFinished;
            }

            public void Shutdown()
            {
            }

            #endregion

            void OnErrorRaised(object sender, BuildErrorEventArgs e)
            {
                string line = $"ERROR: {e.File}({e.LineNumber},{e.ColumnNumber}): ";
                WriteLineWithSenderAndMessage(line, e);
            }

            void OnWarningRaised(object sender, BuildWarningEventArgs e)
            {
                string line = $"Warning: {e.File}({e.LineNumber},{e.ColumnNumber}): ";
                WriteLineWithSenderAndMessage(line, e);
            }

            void OnMessageRaised(object sender, BuildMessageEventArgs e)
            {
                if (Verbosity >= _importanceToVerbosity[e.Importance])
                {
                    WriteLineWithSenderAndMessage(string.Empty, e);
                }
            }

            void OnProjectStarted(object sender, ProjectStartedEventArgs e)
            {
                WriteLine(e.Message);
                _indent++;
            }

            void OnProjectFinished(object sender, ProjectFinishedEventArgs e)
            {
                _indent--;
                WriteLine(e.Message);
            }

            void WriteLineWithSenderAndMessage(string line, BuildEventArgs e)
            {
                // Don't print "MSBuild:" prefix for cleanness.
                bool isMsBuild = e.SenderName.Equals("MSBuild",
                                                     StringComparison.InvariantCultureIgnoreCase);
                string prefix = isMsBuild ? string.Empty : e.SenderName + ": ";
                WriteLine(prefix + line + e.Message);
            }

            void WriteLine(string line)
            {
                for (int n = 0; n < _indent; ++n)
                {
                    _log.Append("\t");
                }

                _log.AppendLine(line);
            }
        }
    }
}
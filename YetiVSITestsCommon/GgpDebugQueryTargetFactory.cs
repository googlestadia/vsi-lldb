// Copyright 2021 Google LLC
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

using Google.VisualStudioFake.API;
using Microsoft.VisualStudio.ProjectSystem.Debug;
using Microsoft.VisualStudio.ProjectSystem.VS.Debug;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using YetiCommon;
using YetiVSI;
using YetiVSI.ProjectSystem.Abstractions;

namespace YetiVSITestsCommon
{
    public class GgpDebugQueryTargetWrapperFactory : IDebugQueryTargetFactory
    {
        /// <summary>
        /// Implementation of Google.VisualStudioFake.API.IDebugQueryTarget that relies on a
        /// GgpDebugQueryTarget underlying member value. It ensures the underlying
        /// GgpDebugQueryTarget is invoked from the correct task context.
        /// </summary>
        class GgpDebugQueryTargetWrapper : Google.VisualStudioFake.API.IDebugQueryTarget
        {
            readonly GgpDebugQueryTarget _ggpDebugQueryTarget;
            readonly JoinableTaskContext _taskContext;
            readonly ManagedProcess.Factory _processFactory;

            internal GgpDebugQueryTargetWrapper(GgpDebugQueryTarget ggpDebugQueryTarget,
                JoinableTaskContext taskContext, ManagedProcess.Factory processFactory)
            {
                _ggpDebugQueryTarget = ggpDebugQueryTarget;
                _taskContext = taskContext;
                _processFactory = processFactory;
            }

            #region Google.VisualStudioFake.API.IDebugQueryTarget

            public IReadOnlyList<IDebugLaunchSettings> QueryDebugTargets(
                IAsyncProject project, DebugLaunchOptions launchOptions)
            {
                IReadOnlyList<IDebugLaunchSettings> settings = null;
                _taskContext.Factory.Run(async () =>
                {
                    await _taskContext.Factory.SwitchToMainThreadAsync();
                    settings = await _ggpDebugQueryTarget.QueryDebugTargetsAsync(
                        project, launchOptions);
                });
                if (!settings.Any())
                {
                    throw new InvalidOperationException("Unable to acquire launch settings. " +
                        "Make sure you have a single reserved instance, and that it is in a " +
                        $"healthy state. {{`ggp instance list` = \"{GetInstanceListOutput()}\"}}");
                }
                return settings;
            }

            #endregion

            string GetInstanceListOutput()
            {
                var process = _processFactory.Create(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ggp",
                    Arguments = "instance list"
                });
                List<string> output = null;
                try
                {
                    _taskContext.Factory.Run(async () =>
                    {
                        output = await process.RunToExitWithSuccessCapturingOutputAsync();
                    });
                }
                catch (ProcessExecutionException e)
                {
                    return string.Join(Environment.NewLine, e.ErrorLines);
                }
                catch (ProcessException e)
                {
                    return e.Message;
                }
                return string.Join(Environment.NewLine, output);
            }
        }

        readonly GgpDebugQueryTarget _ggpDebugQueryTarget;
        readonly JoinableTaskContext _taskContext;
        readonly ManagedProcess.Factory _processFactory;

        public GgpDebugQueryTargetWrapperFactory(GgpDebugQueryTarget ggpDebugQueryTarget,
            JoinableTaskContext taskContext, ManagedProcess.Factory processFactory)
        {
            _ggpDebugQueryTarget = ggpDebugQueryTarget;
            _taskContext = taskContext;
            _processFactory = processFactory;
        }

        public Google.VisualStudioFake.API.IDebugQueryTarget Create() =>
            new GgpDebugQueryTargetWrapper(_ggpDebugQueryTarget, _taskContext, _processFactory);
    }
}

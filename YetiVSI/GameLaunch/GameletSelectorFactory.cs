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

using GgpGrpc.Cloud;
using Microsoft.VisualStudio.Threading;
using YetiVSI.Metrics;

namespace YetiVSI.GameLaunch
{
    public interface IGameletSelectorFactory
    {
        IGameletSelector Create(ActionRecorder actionRecorder);
    }

    public class GameletSelectorFactory : IGameletSelectorFactory
    {
        readonly IDialogUtil _dialogUtil;
        readonly ICloudRunner _runner;
        readonly InstanceSelectionWindow.Factory _gameletSelectionWindowFactory;
        readonly CancelableTask.Factory _cancelableTaskFactory;
        readonly IGameletClientFactory _gameletClientFactory;
        readonly ISshManager _sshManager;
        readonly IRemoteCommand _remoteCommand;
        readonly IGameLaunchBeHelper _gameLaunchBeHelper;
        readonly JoinableTaskContext _taskContext;

        public GameletSelectorFactory(IDialogUtil dialogUtil, ICloudRunner runner,
                                      InstanceSelectionWindow.Factory gameletSelectionWindowFactory,
                                      CancelableTask.Factory cancelableTaskFactory,
                                      IGameletClientFactory gameletClientFactory,
                                      ISshManager sshManager, IRemoteCommand remoteCommand,
                                      IGameLaunchBeHelper gameLaunchBeHelper,
                                      JoinableTaskContext taskContext)
        {
            _dialogUtil = dialogUtil;
            _runner = runner;
            _gameletSelectionWindowFactory = gameletSelectionWindowFactory;
            _cancelableTaskFactory = cancelableTaskFactory;
            _gameletClientFactory = gameletClientFactory;
            _sshManager = sshManager;
            _remoteCommand = remoteCommand;
            _gameLaunchBeHelper = gameLaunchBeHelper;
            _taskContext = taskContext;
        }

        public virtual IGameletSelector Create(ActionRecorder actionRecorder) =>
            new GameletSelector(_dialogUtil, _runner, _gameletSelectionWindowFactory,
                                      _cancelableTaskFactory, _gameletClientFactory, _sshManager,
                                      _remoteCommand, _gameLaunchBeHelper, _taskContext,
                                      actionRecorder);
    }
}

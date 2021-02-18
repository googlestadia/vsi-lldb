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

using GgpGrpc.Cloud;
using NSubstitute;
using YetiCommon;
using YetiVSITestsCommon;

namespace YetiVSI.Test.MediumTestsSupport
{
    public class MediumTestGgpDebugQueryTargetCompRoot : GgpDebugQueryTargetCompRoot
    {
        CancelableTask.Factory _cancelableTaskFactory;
        IGameletClientFactory _gameletClientFactory;

        public MediumTestGgpDebugQueryTargetCompRoot(ServiceManager serviceManager,
                                                     IDialogUtil dialogUtil,
                                                     IGameletClientFactory gameletClientFactory =
                                                         null) : base(serviceManager, dialogUtil)
        {
            _gameletClientFactory = gameletClientFactory;
        }

        public override CancelableTask.Factory GetCancelableTaskFactory()
        {
            if (_cancelableTaskFactory == null)
            {
                var taskContext = _serviceManager.GetJoinableTaskContext();
                _cancelableTaskFactory = FakeCancelableTask.CreateFactory(taskContext, false);
            }

            return _cancelableTaskFactory;
        }

        public override IApplicationClientFactory GetApplicationClientFactory() =>
            new ApplicationClientStub.ApplicationClientFakeFactory();

        public override IGameletClientFactory GetGameletClientFactory()
        {
            if (_gameletClientFactory == null)
            {
                _gameletClientFactory = new GameletClientStub.Factory();
            }

            return _gameletClientFactory;
        }

        public override IRemoteCommand GetRemoteCommand(
            ManagedProcess.Factory managedProcessFactory) =>
            Substitute.For<IRemoteCommand>();

        public override Versions.SdkVersion GetSdkVersion() =>
            Versions.SdkVersion.Create("1.60");

        public override ISshManager GetSshManager(ManagedProcess.Factory managedProcessFactory,
                                                  ICloudRunner cloudRunner) =>
            Substitute.For<ISshManager>();
    }
}
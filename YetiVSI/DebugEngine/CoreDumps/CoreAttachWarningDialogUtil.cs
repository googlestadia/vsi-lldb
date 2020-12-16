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

using Microsoft.VisualStudio.Threading;
using System;
using System.Threading.Tasks;

namespace YetiVSI.DebugEngine.CoreDumps
{
    // Indicates that the user has decided to stop attaching to the dump due to a missing build id.
    public class CoreAttachStoppedException : Exception
    {
        public CoreAttachStoppedException() : base()
        {
        }

        public CoreAttachStoppedException(string message) : base(message)
        {
        }

        public CoreAttachStoppedException(string message, Exception e) : base(message, e)
        {
        }
    }

    public class CoreAttachWarningDialogUtil
    {
        readonly JoinableTaskContext _taskContext;
        readonly IDialogUtil _dialogUtil;

        public CoreAttachWarningDialogUtil(JoinableTaskContext taskContext, IDialogUtil dialogUtil)
        {
            _taskContext = taskContext;
            _dialogUtil = dialogUtil;
        }

        public async Task<bool> ShouldAttachWithoutBuildIdAsync()
        {
            await _taskContext.Factory.SwitchToMainThreadAsync();

            return _dialogUtil.ShowYesNoWarning(ErrorStrings.CoreAttachBuildIdMissingWarningMessage,
                                                ErrorStrings.CoreAttachBuildIdMissingWarningTitle);
        }
    }
}

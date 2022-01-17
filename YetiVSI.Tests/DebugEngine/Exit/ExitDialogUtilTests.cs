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

using Grpc.Core;
using NSubstitute;
using NUnit.Framework;
using YetiCommon;
using YetiVSI.DebugEngine.Exit;
using static YetiVSI.DebugEngine.DebugEngine;

namespace YetiVSI.Test.DebugEngine.Exit
{
    [TestFixture]
    class ExitDialogUtilTests
    {
        IDialogUtil dialogUtil;
        ExitDialogUtil exitDialogUtil;

        [SetUp]
        public void SetUp()
        {
            dialogUtil = Substitute.For<IDialogUtil>();
            exitDialogUtil = new ExitDialogUtil(dialogUtil, a => a());
        }

        [Test]
        public void ShowProcessExecution()
        {
            exitDialogUtil.ShowExitDialog(new ProcessExecutionException("hey now", 2));
            dialogUtil.Received().ShowError(ErrorStrings.ProcessExitedUnexpectedly,
                Arg.Is<ProcessExecutionException>(s => s.Message.Contains("hey now")));
        }

        [Test]
        public void ShowRpcExceptionShowsRpcFailureWhenNoPortNumber(
            [Values(StatusCode.DeadlineExceeded, StatusCode.Unavailable)] StatusCode statusCode)
        {
            exitDialogUtil.ShowExitDialog(
                new RpcException(new Status(statusCode, "some detail")));

            dialogUtil.Received().ShowError(ErrorStrings.RpcFailure,
                Arg.Is<RpcException>(s => s.Message.Contains("some detail")));
        }

        [Test]
        public void ShowRpcExceptionShowsRpcFailureMessage()
        {
            exitDialogUtil.ShowExitDialog(
                new RpcException(new Status(StatusCode.FailedPrecondition, "some detail")));

            dialogUtil.Received().ShowError(ErrorStrings.RpcFailure,
                Arg.Is<RpcException>(s => s.Message.Contains("some detail")));
        }

        [Test]
        public void ShowAttachException()
        {
            exitDialogUtil.ShowExitDialog(new AttachException(5, "A message"));

            dialogUtil.Received().ShowError(
                Arg.Is<string>(s => s.Contains("A message")),
                Arg.Is<AttachException>(e => e.Message.Contains("A message")));
        }
    }
}

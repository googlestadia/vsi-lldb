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

﻿using System;
using Microsoft.VisualStudio.Threading;
using System.Windows.Threading;
using YetiVSI;

namespace YetiVSITestsCommon
{
    public static class FakeCancelableTask
    {
        class FakeProgressDialog : IProgressDialog
        {
            public string Message { get; set; }

            private bool canceled;
            private readonly DispatcherFrame dispatcherFrame = new DispatcherFrame();

            public FakeProgressDialog(bool canceled)
            {
                this.canceled = canceled;
                if (canceled)
                {
                    dispatcherFrame.Continue = false;
                }
            }

            public bool ShowModal()
            {
                Dispatcher.PushFrame(dispatcherFrame);
                return !canceled;
            }

            public void Complete()
            {
                dispatcherFrame.Continue = false;
            }
        }

        class FakeProgressDialogFactory : ProgressDialog.Factory
        {
            private readonly bool cancelled;

            public FakeProgressDialogFactory(bool canceled)
                : base()
            {
                this.cancelled = canceled;
            }

            public override IProgressDialog Create(string title, string text)
                => new FakeProgressDialog(cancelled);
        }

        /// <summary>
        /// Create a CancelableTask.Factory that uses a FakeProgressDialogFactory. The
        /// FakeProgressDialogFactory is configured such that the fake dialog created completes
        /// successfully only if canceled is false.
        /// </summary>
        public static CancelableTask.Factory CreateFactory(JoinableTaskContext taskContext,
            bool canceled)
                => new CancelableTask.Factory(taskContext,
                    new FakeProgressDialogFactory(canceled),
                    "Stadia", TimeSpan.Zero, TimeSpan.Zero);
    }


}

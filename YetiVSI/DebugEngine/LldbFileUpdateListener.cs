// Copyright 2022 Google LLC
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
using YetiCommon;

namespace YetiVSI.DebugEngine
{
    /// <summary>
    /// This class reports about modules downloading process during attachment to LLDB target.
    /// To receive  file update events class uses LldbListenerSubscriber.
    /// </summary>
    class LldbFileUpdateListener
    {
        readonly ICancelable _task;
        readonly LldbListenerSubscriber _listenerSubscriber;

        public LldbFileUpdateListener(LldbListenerSubscriber listenerSubscriber,
                                      ICancelable task)
        {
            _listenerSubscriber = listenerSubscriber;
            _task = task;

        }

        public void Subscribe()
        {
            _listenerSubscriber.FileUpdateReceived += ListenerSubscriberOnFileUpdateReceived;
        }

        public void Unsubscribe()
        {
            // clean up the SBListener subscriber
            _listenerSubscriber.FileUpdateReceived -= ListenerSubscriberOnFileUpdateReceived;
        }

        void ListenerSubscriberOnFileUpdateReceived(object sender, FileUpdateReceivedEventArgs args)
        {
            // Progress.Report uses SynchronizationContext and will take care about UI update.
            FileProcessingUpdate update = args.Update;
            switch (update.Method)
            {
                case FileProcessingState.Read:
                    _task.Progress.Report(
                        $"Debugger is attaching:{Environment.NewLine}downloading {update.File} " +
                        $"({ToMegabytes(update.Size):F1} MB)");
                    break;
                case FileProcessingState.Close:
                    _task.Progress.Report(TaskMessages.LoadingModulesDuringAttach);
                    break;
            }

            double ToMegabytes(uint bytes) => ((double)bytes) / 1048576;
        }
    }
}

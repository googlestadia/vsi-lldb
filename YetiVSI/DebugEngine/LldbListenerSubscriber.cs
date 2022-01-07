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

﻿using DebuggerApi;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace YetiVSI.DebugEngine
{
    public interface ILldbListenerSubscriber
    {
        bool IsRunning { get; }

        /// <summary>
        /// Raised on any exception in event processing. This is called from a background thread.
        /// </summary>
        event EventHandler<ExceptionOccuredEventArgs> ExceptionOccured;

        /// <summary>
        /// Raised on the debugger state change. This is called from a background thread.
        /// </summary>
        event EventHandler<StateChangedEventArgs> StateChanged;

        /// <summary>
        /// Raised on every update in file processing (during attach).
        /// This is called from a background thread.
        /// </summary>
        event EventHandler<FileUpdateReceivedEventArgs> FileUpdateReceived;

        /// <summary>
        /// Raised when remote breakpoint has changed.
        /// </summary>
        event EventHandler<BreakpointChangedEventArgs> BreakpointChanged;

        /// <summary>
        /// Spin off a thread that will constantly try to get new events from the SbListener.
        /// </summary>
        void Start();

        void Stop();
    }

    public class LldbListenerSubscriber : ILldbListenerSubscriber
    {
        public bool IsRunning { get; private set; }
        readonly SbListener _lldbListener;
        readonly LldbEventDescriptionParser _descriptionParser = new LldbEventDescriptionParser();
        CancellationTokenSource _tokenSource;

        static readonly uint _listenerIntervalInSeconds = 1;

        /// <summary>
        /// Raised on any exception in event processing. This is called from a background thread.
        /// </summary>
        public virtual event EventHandler<ExceptionOccuredEventArgs> ExceptionOccured;

        /// <summary>
        /// Raised on the debugger state change. This is called from a background thread.
        /// </summary>
        public virtual event EventHandler<StateChangedEventArgs> StateChanged;

        /// <summary>
        /// Raised on every update in file processing (during attach).
        /// This is called from a background thread.
        /// </summary>
        public virtual event EventHandler<FileUpdateReceivedEventArgs> FileUpdateReceived;

        /// <summary>
        /// Raised when remote breakpoint has changed.
        /// </summary>
        public virtual event EventHandler<BreakpointChangedEventArgs> BreakpointChanged;

        public LldbListenerSubscriber(SbListener lldbListener)
        {
            _lldbListener = lldbListener;
        }

        /// <summary>
        /// Spin off a thread that will constantly try to get new events from the SbListener.
        /// </summary>
        public void Start()
        {
            if (IsRunning)
            {
                return;
            }

            _tokenSource = new CancellationTokenSource();
            var task = Task.Run(() => {
                try
                {
                    while (!_tokenSource.IsCancellationRequested)
                    {
                        if (!_lldbListener.WaitForEvent(_listenerIntervalInSeconds,
                                                        out SbEvent lldbEvent))
                        {
                            continue;
                        }

                        if (lldbEvent == null)
                        {
                            return;
                        }

                        var eventType = lldbEvent.GetEventType();
                        if (lldbEvent.IsBreakpointEvent)
                        {
                            BreakpointChanged?.Invoke(
                                null, new BreakpointChangedEventArgs(lldbEvent));
                        }
                        else if ((eventType & EventType.STATE_CHANGED) != 0)
                        {
                            StateChanged?.Invoke(null, new StateChangedEventArgs(lldbEvent));
                        }
                        else if ((eventType & EventType.STRUCTURED_DATA) != 0)
                        {
                            var update = _descriptionParser.Parse<FileProcessingUpdate>(
                                lldbEvent.GetDescription());
                            if (update != null)
                            {
                                FileUpdateReceived
                                    ?.Invoke(null, new FileUpdateReceivedEventArgs(update));
                            }
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    Trace.WriteLine($"Listener was stopped");
                }
                catch (Exception e)
                {
                    Trace.WriteLine($"Internal error: Failed to receive event from listener: {e}");
                    ExceptionOccured?.Invoke(null, new ExceptionOccuredEventArgs(e));
                }
            }, _tokenSource.Token);

            IsRunning = true;
        }

        public void Stop()
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            _tokenSource.Cancel();
        }
    }

    public class StateChangedEventArgs : EventArgs
    {
        public SbEvent Event { get; }

        public StateChangedEventArgs(SbEvent sbEvent)
        {
            Event = sbEvent;
        }
    }

    public class FileUpdateReceivedEventArgs : EventArgs
    {
        public FileProcessingUpdate Update { get; }

        public FileUpdateReceivedEventArgs(FileProcessingUpdate update)
        {
            Update = update;
        }
    }

    public class ExceptionOccuredEventArgs : EventArgs
    {
        public Exception Exception { get; }

        public ExceptionOccuredEventArgs(Exception exception)
        {
            Exception = exception;
        }
    }

    public sealed class BreakpointChangedEventArgs : EventArgs
    {
        public SbEvent Event { get; }

        public BreakpointChangedEventArgs(SbEvent evt)
        {
            Event = evt;
        }
    }
}

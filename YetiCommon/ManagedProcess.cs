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

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace YetiCommon
{
    // Indicates errors running a process
    public class ProcessException : Exception
    {
        public ProcessException(string message) : base(message)
        {
        }

        public ProcessException(string message, Exception e) : base(message, e)
        {
        }
    }

    // A wrapper for System.Diagnostics.Process.
    public interface IProcess : IDisposable
    {
        /// <summary>
        /// The ID of the process, as reported by the OS after the process has been started.
        /// </summary>
        int Id { get; }

        /// <summary>
        /// The name of the process, as reported by the OS after the process has been started.
        /// </summary>
        string ProcessName { get; }

        /// <summary>
        /// The exit code of the process, which is reported after the process exited.
        /// </summary>
        int ExitCode { get; }

        /// <summary>
        /// The starting information which was specified when creating this process.
        /// </summary>
        ProcessStartInfo StartInfo { get; }

        /// <summary>
        /// Event triggered when the process produces output (stdout).
        /// </summary>
        event TextReceivedEventHandler OutputDataReceived;

        /// <summary>
        /// Event triggered when the process produces error output (stderr).
        /// </summary>
        event TextReceivedEventHandler ErrorDataReceived;

        /// <summary>
        /// Standard output stream of the process. This should only be used if the
        /// process was started with the standardOutputReadLine parameter set
        /// to false.
        /// </summary>
        StreamReader StandardOutput { get; }

        /// <summary>
        /// Event triggered when the process exits.
        /// </summary>
        /// <remarks>
        /// The exit event may be triggered asynchronously w.r.t. data received events.
        /// Do not assume that all data has been received when this event is triggered.
        /// </remarks>
        event EventHandler OnExit;

        /// <summary>
        /// Starts the process and returns immediately.
        /// </summary>
        /// <param name="standardOutputReadLine">
        /// Determines if the the process should automatically read standard output by lines and
        /// call the OutputDataReceived for each line. If the parameter is false, the caller
        /// is responsible for consuming the StandardOutput stream of the process.
        /// </param>
        /// <exception cref="ProcessException">
        /// Thrown if there is an error launching the process.
        /// </exception>
        /// <seealso cref="RunToExitAsync"/>
        void Start(bool standardOutputReadLine = true);

        /// <summary>
        /// Attempts to kill the process immediately. OnExit will not be called.
        /// </summary>
        /// <remarks>
        /// If the process has already exited or cannot be killed, then this call is a no-op.
        /// </remarks>
        void Kill();

        /// <summary>
        /// Launches the process and returns a task that waits for the process to exit and yields
        /// the exit code of the process as the result.
        /// </summary>
        /// <exception cref="ProcessException">Thrown if launching the process fails.</exception>
        /// <remarks>
        /// Output and error data handlers are guaranteed to be called before this task completes.
        /// </remarks>
        /// <returns>The process exit code.</returns>
        Task<int> RunToExitAsync();

        /// <summary>
        /// Returns a task that waits for the started process to exit and yields
        /// the exit code of the process as the result.
        /// Throws ProcessException if the wait times out or if an error occurs.
        /// </summary>
        /// <remarks>
        /// Output and error data handlers are guaranteed to be called before this task completes.
        /// </remarks>
        /// <returns>The process exit code.</returns>
        Task<int> WaitForExitAsync();

        /// <summary>
        /// Waits for the started process to exit. Returns false on error. Does not throw.
        /// </summary>
        /// <param name="timeout">Timeout to wait</param>
        /// <returns>True if the process exited.</returns>
        bool WaitForExit(TimeSpan timeout);
    }

    public delegate void TextReceivedEventHandler(object sender, TextReceivedEventArgs args);

    public class TextReceivedEventArgs : EventArgs
    {
        public string Text { get; }

        public TextReceivedEventArgs(string text)
        {
            Text = text;
        }
    }

    // A wrapper for Process that ties the process execution to a local job object.
    public class ManagedProcess : IProcess
    {
        public class Factory
        {
            public virtual IProcess Create(ProcessStartInfo startInfo, int timeoutMs = 30 * 1000) =>
                new ManagedProcess(startInfo, timeoutMs);

            public virtual IProcess CreateVisible(ProcessStartInfo startInfo,
                                                  int timeoutMs = 30 * 1000) =>
                new ManagedProcess(startInfo, timeoutMs, true);
        }

        readonly int _timeoutMs;

        // System structures and functions needed to make a Windows API call that will tie a
        // spawned process's lifetime to a local object.
        public enum JobObjectInfoType
        {
            AssociateCompletionPortInformation = 7,
            BasicLimitInformation = 2,
            BasicUiRestrictions = 4,
            EndOfJobTimeInformation = 6,
            ExtendedLimitInformation = 9,
            SecurityLimitInformation = 5,
            GroupInformation = 11
        }

        // Windows data types reference:
        // https://docs.microsoft.com/en-us/windows/win32/winprog/windows-data-types

        [StructLayout(LayoutKind.Sequential)]
        struct JobObjectBasicLimitInformation
        {
            public Int64 PerProcessUserTimeLimit;
            public Int64 PerJobUserTimeLimit;
            public UInt32 LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public UInt32 ActiveProcessLimit;
            public UIntPtr Affinity;
            public UInt32 PriorityClass;
            public UInt32 SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct IoCounters
        {
            public UInt64 ReadOperationCount;
            public UInt64 WriteOperationCount;
            public UInt64 OtherOperationCount;
            public UInt64 ReadTransferCount;
            public UInt64 WriteTransferCount;
            public UInt64 OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct JobobjectExtendedLimitInformation
        {
            public JobObjectBasicLimitInformation BasicLimitInformation;
            public IoCounters IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr CreateJobObject(IntPtr a, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetInformationJobObject(IntPtr hJob, JobObjectInfoType infoType,
                                                   IntPtr lpJobObjectInfo,
                                                   UInt32 cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        [DllImport("Kernel32")]
        static extern bool CloseHandle(IntPtr handle);

        readonly Process _process;
        readonly IntPtr _handle;

        public ProcessStartInfo StartInfo => _process.StartInfo;

        public int ExitCode => _process.ExitCode;

        // Local copies of process info to print during Dispose/Finalize.
        public string ProcessName { get; private set; }
        public int Id { get; private set; }

        public event TextReceivedEventHandler OutputDataReceived;
        public event TextReceivedEventHandler ErrorDataReceived;
        public event EventHandler OnExit;

        bool _disposed;

        ManagedProcess(ProcessStartInfo startInfo, int timeoutMs, bool showWindow = false)
        {
            _process = new Process { StartInfo = startInfo };
            _timeoutMs = timeoutMs;

            if (showWindow)
            {
                // When launching the process, show the window. Don't redirect standard output so
                // it can appear in the console window if applicable, but still redirect standard
                // error so errors are logged.
                _process.StartInfo.RedirectStandardInput = false;
                _process.StartInfo.RedirectStandardOutput = false;
                _process.StartInfo.RedirectStandardError = true;
                _process.StartInfo.UseShellExecute = false;
                _process.StartInfo.CreateNoWindow = false;
            }
            else
            {
                _process.StartInfo.RedirectStandardInput = true;
                _process.StartInfo.RedirectStandardOutput = true;
                _process.StartInfo.RedirectStandardError = true;
                _process.StartInfo.UseShellExecute = false;
                _process.StartInfo.CreateNoWindow = true;
            }

            _process.OutputDataReceived += OutputHandler;
            _process.ErrorDataReceived += ErrorHandler;

            _process.EnableRaisingEvents = true;
            _process.Exited += ExitHandler;

            _handle = CreateJobObject(IntPtr.Zero, null);
            var info = new JobObjectBasicLimitInformation
            {
                LimitFlags = 0x2000
            };

            var extendedInfo = new JobobjectExtendedLimitInformation
            {
                BasicLimitInformation = info
            };

            int length = Marshal.SizeOf(typeof(JobobjectExtendedLimitInformation));
            IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
            Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);

            if (!SetInformationJobObject(_handle, JobObjectInfoType.ExtendedLimitInformation,
                                         extendedInfoPtr, (uint) length))
            {
                throw new Exception(
                    ErrorStrings.FailedToSetJobLimitInfo(Marshal.GetLastWin32Error()));
            }
        }

        ~ManagedProcess()
        {
            Dispose(false);
        }

        // Attempts to start the process and returns as soon as it has started.
        // Throws ProcessException if the process cannot be started.
        public void Start(bool standardOutputReadLine = true)
        {
            try
            {
                _process.Start();
                Id = _process.Id;
                ProcessName = Path.GetFileName(_process.StartInfo.FileName);
                AssignProcessToJobObject(_handle, _process.Handle);
                Trace.WriteLine($"Started {_process.StartInfo.FileName} " +
                                $"{_process.StartInfo.Arguments} with id {Id}");
                if (_process.StartInfo.RedirectStandardError)
                {
                    _process.BeginErrorReadLine();
                }

                if (_process.StartInfo.RedirectStandardOutput && standardOutputReadLine)
                {
                    _process.BeginOutputReadLine();
                }
            }
            catch (Exception e) when (e is InvalidOperationException || e is Win32Exception)
            {
                string name = Path.GetFileName(_process.StartInfo.FileName);
                Trace.WriteLine($"Error launching {name}: {e}");
                throw new ProcessException(
                    ErrorStrings.FailedToLaunchProcess(_process.StartInfo.FileName, e.Message), e);
            }
        }

        // Attempts to start the process and returns a task that is completed when the process
        // exits. The result of the task is the exit code of the process.
        // Throws ProcessException if the process cannot be started.
        //
        // The resulting task may fail with a ProcessException if the process does not exit within
        // the timeout specified at construction time.
        public async Task<int> RunToExitAsync()
        {
            Start();
            return await WaitForExitAsync();
        }

        public async Task<int> WaitForExitAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    if (!_process.WaitForExit(_timeoutMs))
                    {
                        Trace.WriteLine($"Timeout waiting for {ProcessName} [{Id}]");
                        throw new ProcessException(
                            ErrorStrings.TimeoutWaitingForProcess(ProcessName));
                    }

                    // WaitForExit(int) does not guarantee that data received handlers
                    // completed. Instead, the documentation tells us to call WaitForExit().
                    _process.WaitForExit();
                });
                return _process.ExitCode;
            }
            catch (Exception e) when (e is InvalidOperationException || e is Win32Exception)
            {
                Trace.WriteLine($"Error waiting for {ProcessName} [{Id}]: {e}");
                throw new ProcessException(
                    ErrorStrings.ErrorWaitingForProcess(ProcessName, e.Message), e);
            }
        }

        public bool WaitForExit(TimeSpan timeout)
        {
            try
            {
                return _process.WaitForExit((int) timeout.TotalMilliseconds);
            }
            catch (Exception e) when (e is SystemException || e is Win32Exception)
            {
                Trace.WriteLine($"Error waiting for {ProcessName} [{Id}]: {e}");
                return false;
            }
        }

        public void Kill()
        {
            _process.Exited -= ExitHandler;

            try
            {
                Trace.WriteLine($"Killing process {ProcessName} [{Id}]");
                _process.Kill();
            }
            catch (Exception e) when (e is InvalidOperationException || e is Win32Exception)
            {
                Trace.WriteLine($"Couldn't kill process {ProcessName} [{Id}]," +
                                " probably already stopping or stopped");
            }
        }

        void OutputHandler(object sender, DataReceivedEventArgs args)
        {
            if (_disposed)
            {
                return;
            }

            if (OutputDataReceived != null)
            {
                OutputDataReceived(this, new TextReceivedEventArgs(args.Data));
            }
            else
            {
                Trace.WriteLine($"{ProcessName} [{Id}] stdout> {args.Data}");
            }
        }

        void ErrorHandler(object sender, DataReceivedEventArgs args)
        {
            if (_disposed)
            {
                return;
            }

            if (ErrorDataReceived != null)
            {
                ErrorDataReceived(this, new TextReceivedEventArgs(args.Data));
            }
            else
            {
                Trace.WriteLine($"{ProcessName} [{Id}] stderr> {args.Data}");
            }
        }

        void ExitHandler(object sender, EventArgs args)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                Trace.WriteLine($"Process {ProcessName} [{Id}] exited with code {ExitCode}");
            }
            // This should never happen, but for some reason it does, see (internal).
            catch (InvalidOperationException exception)
            {
                Trace.WriteLine($"Failed to read an exit code of the process {ProcessName} " +
                    $"[{Id}] due to `{exception.Message}`, the process is already disposed.");
            }

            OnExit?.Invoke(this, args);
        }

        public StreamReader StandardOutput => _process.StandardOutput;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            Trace.WriteLine($"Dispose ({disposing}) job for process {ProcessName} [{Id}]");
            if (disposing)
            {
                _process.Dispose();
            }

            CloseHandle(_handle);
        }
    }
}
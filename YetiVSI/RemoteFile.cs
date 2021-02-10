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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Tasks;
using YetiCommon;
using YetiCommon.SSH;
using YetiVSI.DebugEngine;
using YetiVSI.DebugEngine.Interfaces;

namespace YetiVSI
{
    /// <summary>
    /// Indicates errors when sending the compressed copy.
    /// </summary>
    public class CompressedCopyException : System.Exception
    {
        public CompressedCopyException(string message) : base(message)
        {
        }

        public CompressedCopyException(string message, System.Exception e) : base(message, e)
        {
        }
    }

    public enum DeployCompression
    {
        Uncompressed,
        Compressed,
    }

    // Performs file operations on a remote gamelet.
    public interface IRemoteFile
    {
        // Transfers the provided local file to the provided gamelet, under remotePath.  Throws a
        // ProcessException if the copy operation fails.
        Task<long> PutAsync(SshTarget target, string file, string remotePath,
                            DeployCompression compression, IIncrementalProgress progress,
                            ICancelable task);

        // Gets the specified file from the provided gamelet. Throws a ProcessException if the
        // copy operation fails.
        Task GetAsync(SshTarget target, string file, string destination, ICancelable task);
    }

    public class RemoteFile : IRemoteFile
    {
        readonly ManagedProcess.Factory _remoteProcessFactory;
        readonly DebugEngine.LldbTransportSession.Factory _transportSessionFactory;
        readonly ILocalSocketSender _localSocketSender;
        readonly IFileSystem _fileSystem;

        public RemoteFile(ManagedProcess.Factory remoteProcessFactory,
                          DebugEngine.LldbTransportSession.Factory transportSessionFactory,
                          ILocalSocketSender socketSender, IFileSystem fileSystem)
        {
            _remoteProcessFactory = remoteProcessFactory;
            _transportSessionFactory = transportSessionFactory;
            _localSocketSender = socketSender;
            _fileSystem = fileSystem;
        }

        public async Task<long> PutAsync(SshTarget target, string file, string remotePath,
                                         DeployCompression compression,
                                         IIncrementalProgress progress, ICancelable task)
        {
            ProcessManager processManager = ProcessManager.CreateForCancelableTask(task);
            long transferredBytes = 0;
            switch (compression)
            {
                case DeployCompression.Uncompressed:
                    await ScpAsync(ProcessStartInfoBuilder.BuildForScpPut(file, target, remotePath),
                                   processManager);
                    transferredBytes = FileUtil.GetFileSize(file, _fileSystem);
                    break;
                case DeployCompression.Compressed:
                    transferredBytes = await PutCompressedAsync(target, file, remotePath, progress,
                                                                processManager, task);
                    break;
            }

            // Notify client if operation was cancelled.
            task.ThrowIfCancellationRequested();
            return transferredBytes;
        }

        public async Task GetAsync(SshTarget target, string file, string destination,
                                   ICancelable task)
        {
            await ScpAsync(ProcessStartInfoBuilder.BuildForScpGet(file, target, destination),
                           ProcessManager.CreateForCancelableTask(task));

            // Notify client if operation was cancelled.
            task.ThrowIfCancellationRequested();
        }

        async Task ScpAsync(ProcessStartInfo startInfo, ProcessManager processManager)
        {
            // TODO ((internal)) : Instead of showing the command window, we should find someway to
            // parse stdout, or use an SSH library, and update the dialog window progress bar.
            using (var process = _remoteProcessFactory.CreateVisible(startInfo, int.MaxValue))
            {
                processManager.AddProcess(process);
                await process.RunToExitWithSuccessAsync();
            }
        }

        IProcess CreateUncompressProcess(ProcessStartInfoBuilder.PortForwardEntry portEntry,
                                         string remotePath, SshTarget target)
        {
            // On the remote server we run the following commands concurrently:
            // - netcat to receive the compressed stream from a socket, passing it to stdout.
            // - gzip to uncompress the stream.
            // - tee to store the uncompressed data to a file and to pass the data to stdout
            //   for chekcsumming.
            // - md5sum to compute the checksum and write it out to stdout.
            string command = $"nc -vlp {portEntry.RemotePort} | " + $"gzip -d --stdout | " +
                $"tee {ProcessUtil.QuoteAndEscapeArgumentForSsh(remotePath)} | " + $"md5sum -b";

            var ports = new ProcessStartInfoBuilder.PortForwardEntry[] { portEntry };

            ProcessStartInfo decompressorStartInfo =
                ProcessStartInfoBuilder.BuildForSshPortForwardAndCommand(ports, target, command);

            return _remoteProcessFactory.Create(decompressorStartInfo, int.MaxValue);
        }

        async Task<long> PutCompressedAsync(SshTarget target, string localPath, string remotePath,
                                            IIncrementalProgress progress,
                                            ProcessManager processManager, ICancelable task)
        {
            // The compressed transfer uses a socket that is tunneled through ssh.

            // On the client, we run a parallel gzip (i.e., pigz), transferring the data
            // from its stdout to the tunneled socket.

            // On the server side, we read the socket and decompress the stream using gzip.

            // We also compute MD5 hash on the client and the server. We only succeed if
            // the hashes match.

            using (ITransportSession transportSession = _transportSessionFactory.Create())
            {
                if (transportSession == null)
                {
                    throw new CompressedCopyException("Number of concurrent sessions exceeded");
                }

                var portEntry = new ProcessStartInfoBuilder.PortForwardEntry
                {
                    LocalPort = transportSession.GetReservedLocalAndRemotePort(),
                    RemotePort = transportSession.GetReservedLocalAndRemotePort(),
                };

                using (IProcess uncompressProcess =
                    CreateUncompressProcess(portEntry, remotePath, target))
                {
                    processManager.AddProcess(uncompressProcess);
                    return await RunTransferProcessesAsync(localPath, portEntry.LocalPort,
                                                           uncompressProcess, progress,
                                                           processManager, task);
                }
            }
        }

        /// <returns>
        /// Size of transferred data in bytes.
        /// </returns>
        async Task<long> RunTransferProcessesAsync(string localPath, int localPort,
                                                   IProcess uncompressProcess,
                                                   IIncrementalProgress progress,
                                                   ProcessManager manager, ICancelable task)
        {
            // Start the remote processes and Wait for the first line of netcat's standard
            // output. This should start with "Listening" once it is ready to accept
            // a connection.
            var firstLineEvent = new TaskCompletionSource<string>();
            var uncompressOutputLines = new List<string>();
            uncompressProcess.OutputDataReceived += (object sender, TextReceivedEventArgs args) =>
            {
                // Netcat will send a line starting with "Listening" once it sets up
                // the TCP listener. Let us notify the main task once that happens.
                firstLineEvent.TrySetResult(args.Text);

                // We also make sure to collect the entire output of the command
                // so that we can later parse the MD5 hash from it.
                if (args.Text != null)
                {
                    uncompressOutputLines.Add(args.Text);
                    Trace.WriteLine($"{uncompressProcess.ProcessName} " +
                                    $"[{uncompressProcess.Id}] stdout> {args.Text}");
                }
            };
            uncompressProcess.Start(standardOutputReadLine: true);

            // Wait until the remote netcat process starts listening.
            string firstLine = await firstLineEvent.Task;
            if (firstLine == null || !firstLine.StartsWith("Listening"))
            {
                uncompressProcess.Kill();
                throw new CompressedCopyException(
                    $"Unexpected output from the remote transfer receiver process: '{firstLine}'");
            }

            // Once netcat starts listening, launch also the compress process and
            // the hash process in parallel.
            Task<long> compressTask = RunCompressProcessAsync(localPath, localPort, progress,
                                                              manager, task);

            string hash = await ComputeFileHashAsync(localPath, task);
            var sentBytes = await compressTask;

            int uncompressExitCode = await uncompressProcess.WaitForExitAsync();
            uncompressProcess.CheckExitCode(uncompressExitCode);

            CheckBinaryHash(hash, uncompressOutputLines);
            return sentBytes;
        }

        void CheckBinaryHash(string localHash, List<string> remoteProcessOutput)
        {
            // Check the hash we got from the remote process.
            //
            // '''
            // Listening on [0.0.0.0] (family 0, port 12345)
            // Connection from [127.0.0.1] port 12345 [tcp/*] accepted (family 2, sport 55010)
            // 73a60745164414b1e753ef831149de76 *-

            if (remoteProcessOutput.Count < 3 ||
                !remoteProcessOutput[0].StartsWith("Listening on") ||
                !remoteProcessOutput[1].StartsWith("Connection from"))
            {
                throw new CompressedCopyException("Unexpected remote netcat/md5sum output");
            }

            string remoteHash = remoteProcessOutput[2].Length > 0
                ? remoteProcessOutput[2].Split(' ')[0]
                : "";

            if (localHash != remoteHash)
            {
                throw new CompressedCopyException("MD5 hash of remote " +
                                                  "file is different from the hash of the local " +
                                                  $"file ('{remoteHash}' != '{localHash}')");
            }
        }

        async Task<string> ComputeFileHashAsync(string localPath, ICancelable task)
        {
            try
            {
                const long bufferSize = 1024 * 1024;
                byte[] fileBuffer = new byte[bufferSize];
                using (MD5 md5 = MD5.Create())
                {
                    using (Stream file = _fileSystem.FileStream.Create(localPath, FileMode.Open,
                                                                       FileAccess.Read))
                    {
                        while (true)
                        {
                            if (task.IsCanceled)
                            {
                                return string.Empty;
                            }

                            int byteCount = await file.ReadAsync(fileBuffer, 0, fileBuffer.Length,
                                                                 task.Token);
                            if (byteCount == 0)
                            {
                                break;
                            }

                            md5.TransformBlock(fileBuffer, 0, byteCount, null, 0);
                        }

                        md5.TransformFinalBlock(fileBuffer, 0, 0);
                        return BitConverter.ToString(md5.Hash).Replace("-", "").ToLower();
                    }
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                throw new CompressedCopyException("Failed to read the file for computing MD5 hash",
                                                  e);
            }
        }

        /// <returns>
        /// Size of sent data in bytes.
        /// </returns>
        async Task<long> RunCompressProcessAsync(string localPath, int port,
                                                 IIncrementalProgress progress,
                                                 ProcessManager manager, ICancelable task)
        {
            long sentDataSize = 0;
            ProcessStartInfo compressorStartInfo =
                ProcessStartInfoBuilder.BuildForCompress(localPath);

            using (IProcess compressProcess =
                _remoteProcessFactory.Create(compressorStartInfo, int.MaxValue))
            {
                manager.AddProcess(compressProcess);
                compressProcess.Start(standardOutputReadLine: false);
                using (Stream outputStream = compressProcess.StandardOutput.BaseStream)
                {
                    try
                    {
                        // Copy the data over.
                        sentDataSize =
                            await _localSocketSender.SendAsync(outputStream, port, progress, task);
                    }
                    catch (Exception e) when (e is SocketException || e is IOException)
                    {
                        throw new CompressedCopyException(
                            "Failed to transfer the compressed data through the socket", e);
                    }
                }

                // Wait for the processes to finish.
                int compressExitCode = await compressProcess.WaitForExitAsync();

                // Notify client if operation was cancelled.
                // In case of cancelling pigz process will be killed. This will cause CheckExitCode
                // to throw ProcessExecutionException. So cancellation exception should be thrown
                // before.
                task.ThrowIfCancellationRequested();
                compressProcess.CheckExitCode(compressExitCode);
            }

            return sentDataSize;
        }
    }

    /// <summary>
    /// Local socket sender implementation send a stream of data to a local socket.
    /// </summary>
    public interface ILocalSocketSender
    {
        /// <summary>
        /// Sends the |data| stream to a server at local port |port|.
        /// Reports progress to |progress|.
        /// </summary>
        /// <returns>
        /// Size of sent |data| in bytes.
        /// </returns>
        /// <exception cref="SocketException">
        /// Thrown if connection to the socket returns an error.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown if the stream copy fails.
        /// </exception>
        Task<long> SendAsync(Stream data, int port, IIncrementalProgress progress,
                             ICancelable task);
    }

    public class LocalSocketSender : ILocalSocketSender
    {
        public async Task<long> SendAsync(Stream data, int port, IIncrementalProgress progress,
                                          ICancelable task)
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, port);
            SocketError result;

            var connectCompletion = new TaskCompletionSource<SocketError>();
            Socket socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            var socketEventArgs = new SocketAsyncEventArgs();
            socketEventArgs.RemoteEndPoint = endPoint;
            socketEventArgs.Completed += (s, e) =>
            {
                connectCompletion.SetResult(socketEventArgs.SocketError);
            };
            socket.ConnectAsync(socketEventArgs);

            result = await connectCompletion.Task;
            if (result != SocketError.Success)
            {
                throw new SocketException((int) result);
            }

            using (var networkStream = new NetworkStream(socket, true))
            {
                return await CopyToAsync(data, networkStream, progress, task);
            }
        }

        async Task<long> CopyToAsync(Stream from, Stream to, IIncrementalProgress progress,
                                     ICancelable task)
        {
            // This is default buffer size for Stream.CopyToAsync.
            // See https://docs.microsoft.com/en-us/dotnet/api/system.io.stream.copytoasync
            const long defaultBufferSize = 80 * 1024;
            var buffer = new byte[defaultBufferSize];
            int count;
            long transferred = 0;
            while ((count = await from.ReadAsync(buffer, 0, buffer.Length, task.Token)) != 0)
            {
                if (task.IsCanceled)
                {
                    return 0;
                }

                transferred += count;
                await to.WriteAsync(buffer, 0, count);
                progress.ReportProgressDelta(count);
            }

            return transferred;
        }
    }
}
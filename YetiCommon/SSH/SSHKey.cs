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
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using YetiCommon;

namespace YetiCommon.SSH
{
    // Represents an error accessing an SSH key.
    public class SshKeyException : Exception
    {
        public SshKeyException(string message) : base(message) { }
    }

    // Representation of an SSH key stored on disk.
    public struct SshKey
    {
        public string PublicKey { get; set; }
    }

    // An interface for accessing an SSH key on disk.
    public interface ISshKeyLoader
    {
        // Loads the dev SSH key stored on disk, creating one if it doesn't exist.
        // Throws an SshKeyException if a key cannot be returned.
        Task<SshKey> LoadOrCreateAsync();
    }

    public class SshKeyLoader : ISshKeyLoader
    {
        readonly ManagedProcess.Factory managedProcessFactory;
        readonly string keyPath;

        public SshKeyLoader(ManagedProcess.Factory managedProcessFactory)
            : this(managedProcessFactory, SDKUtil.GetSshKeyFilePath()) { }

        public SshKeyLoader(ManagedProcess.Factory managedProcessFactory, string keyPath)
        {
            this.managedProcessFactory = managedProcessFactory;
            this.keyPath = keyPath;
        }

        public async Task<SshKey> LoadOrCreateAsync()
        {
            var keyDir = Path.GetDirectoryName(keyPath);
            try
            {
                Directory.CreateDirectory(keyDir);
            }
            catch (Exception e)
            {
                Trace.WriteLine("Failed to create keys directory: " + e.ToString());
                throw new SshKeyException(ErrorStrings.FailedToCreateSshKeysDirectory(e.Message));
            }

            if (!File.Exists(keyPath))
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(SDKUtil.GetSshPath(),
                        YetiConstants.SshKeygenWinExecutable),
                    Arguments = "-f \"" + keyPath + "\" -t rsa -N \"\"",
                };
                using (var process = managedProcessFactory.Create(startInfo))
                {
                    try
                    {
                        int code = await process.RunToExitAsync();
                        if (code != 0)
                        {
                            Trace.WriteLine("Key generation returned code: " + code);
                            throw new SshKeyException(ErrorStrings.SshKeyGenerationFailed(
                                    YetiConstants.SshKeygenWinExecutable, code));
                        }
                    }
                    catch (ProcessException e)
                    {
                        Trace.WriteLine("Key generation threw: " + e.ToString());
                        throw new SshKeyException(
                            ErrorStrings.FailedToRunSshKeyGeneration(e.Message));
                    }
                }
            }
            try
            {
                return new SshKey { PublicKey = File.ReadAllText(keyPath + ".pub") };
            }
            catch (Exception e)
            {
                Trace.WriteLine("Failed to read key file: " + e.ToString());
                throw new SshKeyException(ErrorStrings.FailedToReadSshKeyFile(e.Message));
            }
        }
    }
}

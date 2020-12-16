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
using System.Linq;
using GgpGrpc.Models;

namespace YetiCommon.SSH
{
    // Represents an error accessing SSH known hosts.
    public class SshKnownHostsException : Exception
    {
        public SshKnownHostsException(string message) : base(message)
        {
        }
    }

    // An interface for write SSH known_hosts to disk.
    public interface ISshKnownHostsWriter
    {
        // Store the SSH known_hosts to disk, or updates them if they already exist.  Throws an
        // SshKeyException if a key cannot be returned.
        void CreateOrUpdate(Gamelet gamelet);
    }

    public class SshKnownHostsWriter : ISshKnownHostsWriter
    {
        readonly string path;

        public SshKnownHostsWriter() : this(SDKUtil.GetSshKnownHostsFilePath())
        {
        }

        public SshKnownHostsWriter(string path)
        {
            this.path = path;
        }

        public void CreateOrUpdate(Gamelet gamelet)
        {
            var sshDir = Path.GetDirectoryName(path);
            try
            {
                Directory.CreateDirectory(sshDir);
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Failed to create ssh directory: {e}");
                throw new SshKeyException(ErrorStrings.FailedToCreateSshDirectory(e.Message));
            }

            var target = new SshTarget(gamelet);
            var host = gamelet.IpAddr;
            if (target.Port != 22)
            {
                host = $"[{host}]:{target.Port}";
            }

            var lines = gamelet.PublicKeys.Select(hostKey =>
                $"{host} {AlgorithmToString(hostKey.Algorithm)} {hostKey.PublicKey}");

            try
            {
                File.WriteAllLines(path, lines);
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Failed to write known_hosts file: {e}");
                throw new SshKnownHostsException(
                    ErrorStrings.FailedToWriteKnownHostsFile(e.Message));
            }
        }

        private string AlgorithmToString(SshKeyGenAlgorithm sshKeyGenAlgorithm)
        {
            switch (sshKeyGenAlgorithm)
            {
                case SshKeyGenAlgorithm.Ed25519:
                    return "ssh-ed25519";
                case SshKeyGenAlgorithm.Ecdsa:
                    return "ecdsa-sha2-nistp256";
                case SshKeyGenAlgorithm.Rsa:
                    return "ssh-rsa";
                case SshKeyGenAlgorithm.Dsa:
                    return "ssh-dss";
                default:
                    return "unknown";
            }
        }
    }
}
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
using System.Security;
using System.Threading;
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
        // Store the SSH known_hosts to disk, or updates them if they already exist.
        // Throws an SshKeyException if a key cannot be returned.
        void CreateOrUpdate(Gamelet gamelet);
    }

    public class SshKnownHostsWriter : ISshKnownHostsWriter
    {
        readonly string _path;

        const int _knownHostsOpenTries = 50;

        public SshKnownHostsWriter() : this(SDKUtil.GetSshKnownHostsFilePath())
        {
        }

        public SshKnownHostsWriter(string path)
        {
            _path = path;
        }

        public void CreateOrUpdate(Gamelet gamelet)
        {
            var sshDir = Path.GetDirectoryName(_path);
            try
            {
                Directory.CreateDirectory(sshDir);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Trace.WriteLine($"Failed to create ssh directory: {e}");
                throw new SshKeyException(ErrorStrings.FailedToCreateSshDirectory(e.Message));
            }

            try
            {
                // Try opening the file a few times on Windows. While os.OpenFile sets the proper
                // sharing rights, apparently other tools might not, like ssh.exe, so a sharing
                // violation is possible. These tools should only hold exclusive access for a
                // very short time, though.
                FileStream fs = null;
                for (int n = 0; n < _knownHostsOpenTries; ++n)
                {
                    try
                    {
                        fs = new FileStream(_path, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                                            FileShare.None /* Lock this file! */);
                        break;
                    }
                    catch (IOException e)
                    {
                        if (n + 1 == _knownHostsOpenTries)
                        {
                            Trace.WriteLine($"Failed to open '{_path}', giving up: {e}");
                            throw;
                        }

                        Trace.WriteLine($"Failed to open '{_path}', retrying...");
                        Thread.Sleep(TimeSpan.FromMilliseconds(1));
                    }
                }

                using (fs)
                {
                    // Read known_hosts file, separating lines referring to the given gamelet from
                    // all other lines.
                    List<string> otherLines = new List<string>();
                    List<string> gameletLines = new List<string>();
                    string prefix = $"[{gamelet.IpAddr}]";

                    // Don't use "using", it would close the fs when done.
                    StreamReader reader = new StreamReader(fs);
                    for (string line = reader.ReadLine(); line != null; line = reader.ReadLine())
                    {
                        if (line.StartsWith(prefix))
                        {
                            gameletLines.Add(line);
                        }
                        else
                        {
                            otherLines.Add(line);
                        }
                    }

                    // Append keys for the gamelet.
                    var target = new SshTarget(gamelet);
                    var host = gamelet.IpAddr;
                    if (target.Port != 22)
                    {
                        host = $"[{host}]:{target.Port}";
                    }

                    List<string> newGameletLines = new List<string>();
                    foreach (SshHostPublicKey pk in gamelet.PublicKeys)
                    {
                        newGameletLines.Add(
                            $"{host} {AlgorithmToString(pk.Algorithm)} {pk.PublicKey}");
                    }

                    // Early out if there are no changes.
                    if (gameletLines.SequenceEqual(newGameletLines))
                    {
                        return;
                    }

                    // Rewrite file.
                    fs.SetLength(0);
                    using (StreamWriter writer = new StreamWriter(fs))
                    {
                        foreach (string line in otherLines)
                        {
                            writer.WriteLine(line);
                        }

                        foreach (string line in newGameletLines)
                        {
                            writer.WriteLine(line);
                        }
                    }
                }
            }
            catch (Exception e) when (e is SecurityException || e is IOException ||
                e is UnauthorizedAccessException)
            {
                Trace.WriteLine($"Failed to write known_hosts file: {e}");
                throw new SshKnownHostsException(
                    ErrorStrings.FailedToWriteKnownHostsFile(e.Message));
            }
        }

        string AlgorithmToString(SshKeyGenAlgorithm sshKeyGenAlgorithm)
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
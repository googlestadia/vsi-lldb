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

﻿using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace YetiVSI.Test
{
    [TestFixture]
    class SshTunnelsTests
    {
        [Test]
        public void ExtractMountingPointsOnEmptyCollectionReturnsEmptyCollection()
        {
            var sshTunnels = new SshTunnels();
            IEnumerable<string> mountingPoints =
                sshTunnels.ExtractMountingPoints(new List<string>());
            Assert.That(mountingPoints, Is.Empty);
        }

        [Test]
        public void ExtractMountingPointsCorrectlyParsesFolderFromCommandLines()
        {
            var sshTunnels = new SshTunnels();
            var commandLines = new List<string> {
                @"""C:\Program Files\OpenSSH\sshd.exe"" ""C:\Program Files\GGP SDK\tools\OpenSSH-Win64\sshd"" -D -e -f NUL -h C:\Users\ldap\AppData\Roaming\GGP\sshd\assets_streamer_host_ecdsa -p 44433 -o ListenAddress=localhost -o MaxSessions=1 -o ""ForceCommand=internal-sftp -P symlink,hardlink"" -o Subsystem=sftp=internal-sftp -o ChrootDirectory=\""c:/src/tests\"" -o DisableForwarding=yes -o PasswordAuthentication=no -o AuthorizedKeysFile=C:\Users\ldap\AppData\Roaming\GGP\sshd\authorized_keys_44433  ",
                @"""C:\Program Files\OpenSSH\sshd.exe"" ""C:\Program Files\GGP SDK\tools\OpenSSH-Win64\sshd"" -D -e -f NUL -h C:\Users\ldap\AppData\Roaming\GGP\sshd\assets_streamer_host_ecdsa -p 44433 -o ListenAddress=localhost -o MaxSessions=1 -o ""ForceCommand=internal-sftp -P symlink,hardlink"" -o Subsystem=sftp=internal-sftp -o ChrootDirectory=\""c:/src/tests\"" -o DisableForwarding=yes -o PasswordAuthentication=no -o AuthorizedKeysFile=C:\Users\ldap\AppData\Roaming\GGP\sshd\authorized_keys_44433 -R ",
                "",
                @"""C:\Program Files\OpenSSH\sshd.exe""",
                @"""C:\Program Files\OpenSSH\sshd.exe"" ""C:\Program Files\GGP SDK\tools\OpenSSH-Win64\sshd"" -D -e -f NUL -h C:\Users\ldap\AppData\Roaming\GGP\sshd\assets_streamer_host_ecdsa -p 44433 -o ListenAddress=localhost -o MaxSessions=1 -o ""ForceCommand=internal-sftp -P symlink,hardlink"" -o Subsystem=sftp=internal-sftp -o ChrootDirectory=\""c:/src/tests\"" -o DisableForwarding=yes -o PasswordAuthentication=no -o AuthorizedKeysFile=C:\Users\ldap\AppData\Roaming\GGP\sshd\authorized_keys_44433 -z "
            };
            IEnumerable<string> mountingPoints =
                sshTunnels.ExtractMountingPoints(commandLines).ToArray();

            var expectedMountingPoints = new List<string>() {
                @"\""c:/src/tests\""",
                @"\""c:/src/tests\""",
                @"\""c:/src/tests\""",
            };

            CollectionAssert.AreEqual(expectedMountingPoints, mountingPoints);
        }
    }
}

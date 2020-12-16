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

using GgpGrpc.Models;

namespace YetiCommon.SSH
{
    public class SshTarget
    {
        public readonly string IpAddress;

        public readonly int Port;

        public SshTarget(Gamelet gamelet)
        {
            IpAddress = gamelet.IpAddr;
            if (gamelet.Id.StartsWith("devkit"))
            {
                Port = 22;
            }
            else
            {
                Port = 44722;
            }
        }

        public SshTarget (string target)
        {
            var parts = target.Split(':');
            IpAddress = parts[0];
            Port = int.Parse(parts[1]);
        }

        public string GetString()
        {
            return IpAddress + ":" + Port;
        }

        public override bool Equals(object obj)
        {
            var item = obj as SshTarget;
            if (item == null)
            {
                return false;
            }
            return IpAddress.Equals(item.IpAddress) && Port.Equals(item.Port);
        }

        public override int GetHashCode()
        {
            return IpAddress.GetHashCode() ^ Port.GetHashCode();
        }
    }
}
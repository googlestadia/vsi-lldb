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

namespace Metrics.Shared
{
    /// <summary>
    /// This class is a stub for a proto. It uses a constant hash
    /// as it is only required to be able to override equality
    /// for testing purposes.
    /// </summary>
    public class ReconnectData
    {
        public class Types
        {
            /// <summary>
            /// Stage where <see cref="MountRemote.RemoteClientImpl.ReconnectIfNeeded"/> exited.
            /// </summary>
            public enum ReconnectStage
            {
                MrReconnectUnknown,
                MrReconnectFunctionInvokedOnce,
                MrReconnectRequested,
                MrReconnectFunctionInvokedTwice
            }

            /// <summary>
            /// Error type generated during reconnect.
            /// </summary>
            public enum ReconnectStatus
            {
                MrReconnectStatusUnknown,
                MrReconnectStatusSuccess,
                MrReconnectStatusSshError,
                MrReconnectStatusOtherError
            }

            /// <summary>
            /// Method where reconnect originated.
            /// </summary>
            public enum ReconnectSource
            {
                MrReconnectSourceUnknown,
                MrReconnectSourceOpen,
                MrReconnectSourceListDirectory,
                MrReconnectSourceGetStatus,
                MrReconnectSourceCreateDirectory,
                MrReconnectSourceGetWorkingDirectory,
                MrReconnectSourceRenameFile,
                MrReconnectSourceGet,
                MrReconnectSourceExists,
                MrReconnectSourceDelete
            }
        }

        public Types.ReconnectStage? ReconnectStage { get; set; }

        public Types.ReconnectStatus? ReconnectStatus { get; set; }

        /// <summary>
        /// Disconnect reason mapped from
        /// <see href="https://doc.neonkube.com/T_Renci_SshNet_Messages_Transport_DisconnectReason.htm"/>.
        /// </summary>
        public int? ReconnectSshStatus { get; set; }

        public Types.ReconnectSource? ReconnectSource { get; set; }

        public override int GetHashCode() => 42;

        public override bool Equals(object other) =>
            Equals(other as ReconnectData);

        public bool Equals(ReconnectData other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(other, this))
            {
                return true;
            }

            if (!Equals(other.ReconnectStage, ReconnectStage))
            {
                return false;
            }

            if (!Equals(other.ReconnectStatus, ReconnectStatus))
            {
                return false;
            }

            if (!Equals(other.ReconnectSshStatus, ReconnectSshStatus))
            {
                return false;
            }

            if (!Equals(other.ReconnectSource, ReconnectSource))
            {
                return false;
            }

            return true;
        }

        public ReconnectData Clone()
        {
            var clone = (ReconnectData) MemberwiseClone();

            return clone;
        }

        public void MergeFrom(ReconnectData other)
        {
            if (other.ReconnectSource.HasValue)
            {
                ReconnectSource = other.ReconnectSource;
            }

            if (other.ReconnectSshStatus.HasValue)
            {
                ReconnectSshStatus = other.ReconnectSshStatus;
            }

            if (other.ReconnectStage.HasValue)
            {
                ReconnectStage = other.ReconnectStage;
            }

            if (other.ReconnectStatus.HasValue)
            {
                ReconnectStatus = other.ReconnectStatus;
            }
        }
    }
}

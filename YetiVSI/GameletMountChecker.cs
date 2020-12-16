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

﻿using GgpGrpc.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using YetiCommon;
using YetiCommon.SSH;
using YetiVSI.Metrics;

namespace YetiVSI
{
    [Flags]
    public enum MountConfiguration
    {
        None = 0,
        Overlay = 1 << 0,
        PackageMounted = 1 << 1,
        RunFromPackage = 1 << 2,
        LocalStreaming = 1 << 3,
    }

    /// <summary>
    /// Provides logic to verify gamelet mount state before running the application.
    /// </summary>
    public class GameletMountChecker
    {
        public const string ReadMountsCmd = "cat /proc/mounts";
        const string _overlayFileSystem = "overlay";
        const string _sshFileSystem = "fuse.sshfs";
        readonly HashSet<string> _keys =
            new HashSet<string> { YetiConstants.GameAssetsMountingPoint,
                                  YetiConstants.PackageMountingPoint,
                                  YetiConstants.DeveloperMountingPoint };
        readonly IRemoteCommand _remoteCommand;
        readonly IDialogUtil _dialogUtil;
        readonly CancelableTask.Factory _cancelableTaskFactory;

        public GameletMountChecker(IRemoteCommand remoteCommand, IDialogUtil dialogUtil,
                                   CancelableTask.Factory cancelableTaskFactory)
        {
            _remoteCommand = remoteCommand;
            _dialogUtil = dialogUtil;
            _cancelableTaskFactory = cancelableTaskFactory;
        }

        readonly StringComparison _comparisonType = StringComparison.OrdinalIgnoreCase;

        public MountConfiguration GetConfiguration(Gamelet gamelet, ActionRecorder actionRecorder)
        {
            MountConfiguration mountConfiguration = MountConfiguration.None;
            List<string> mountsInfo = ReadMountsContentOrDefault(gamelet, actionRecorder);
            if (mountsInfo.Count == 0)
            {
                return mountConfiguration;
            }

            Dictionary<string, Device> devices = GetDevices(mountsInfo);
            Device gameAssetsDevice = devices[YetiConstants.GameAssetsMountingPoint];
            Device developerDevice = devices[YetiConstants.DeveloperMountingPoint];
            Device packageDevice = devices[YetiConstants.PackageMountingPoint];

            if (gameAssetsDevice?.FileSystemType.Equals(_overlayFileSystem, _comparisonType)
                ?? false)
            {
                mountConfiguration |= MountConfiguration.Overlay;
            }

            if (developerDevice?.FileSystemType.Equals(_sshFileSystem, _comparisonType) ?? false)
            {
                mountConfiguration |= MountConfiguration.LocalStreaming;
            }

            if (string.IsNullOrWhiteSpace(packageDevice?.Address))
            {
                if (gameAssetsDevice == null || developerDevice == null)
                {
                    return mountConfiguration;
                }

                if (!gameAssetsDevice.Address.Equals(developerDevice.Address, _comparisonType))
                {
                    mountConfiguration |= MountConfiguration.RunFromPackage;
                }
            }
            else
            {
                mountConfiguration |= MountConfiguration.PackageMounted;
            }

            return mountConfiguration;
        }

        public bool IsGameAssetsDetachedFromDeveloperFolder(
            MountConfiguration currentMountConfiguration) =>
            (currentMountConfiguration == MountConfiguration.RunFromPackage ||
             (currentMountConfiguration.HasFlag(MountConfiguration.PackageMounted) &&
              !currentMountConfiguration.HasFlag(MountConfiguration.Overlay)));

        public bool IsAssetStreamingActivated(MountConfiguration currentMountConfiguration) =>
            currentMountConfiguration.HasFlag(MountConfiguration.LocalStreaming);

        List<string> ReadMountsContentOrDefault(Gamelet gamelet, ActionRecorder actionRecorder)
        {
            List<string> content = new List<string>();
            ICancelableTask getMountsTask =
                _cancelableTaskFactory.Create(TaskMessages.CheckingMountInfo, async _ => {
                    content = await _remoteCommand.RunWithSuccessCapturingOutputAsync(
                        new SshTarget(gamelet), ReadMountsCmd) ?? new List<string>();
                });

            try
            {
                getMountsTask.RunAndRecord(actionRecorder, ActionType.GameletReadMounts);
                return content;
            }
            catch (ProcessException e)
            {
                Trace.WriteLine($"Error reading /proc/mounts file: {e.Message}");
                _dialogUtil.ShowError(ErrorStrings.FailedToStartRequiredProcess(e.Message),
                                      e.ToString());
                return content;
            }
            finally
            {
                string joinedContent = string.Join("\n\t", content);
                Trace.WriteLine($"Gamelet /proc/mounts:{Environment.NewLine}{joinedContent}");
            }
        }

        public class Device
        {
            public Device(string address, string fileSystemType)
            {
                Address = address;
                FileSystemType = fileSystemType;
            }

            public string Address { get; }
            public string FileSystemType { get; }
        }

        /// <summary>
        /// Returns dictionary of the mounting points of interest (/srv/game/assets, /mnt/package,
        /// /mnt/developer) to the devices they are currently mounted to.
        /// Format of /proc/mounts is:
        /// 1st column - device that is mounted;
        /// 2nd column - the mount point;
        /// 3rd column - the file-system type;
        /// 4th column - is mount read-only (ro) or read-write (rw); (5th and 6th are place-holders)
        /// </summary>
        /// <param name="procMountsContent">Content of /proc/mounts file on the gamelet.</param>
        /// <returns>Dictionary of mounting point to device.</returns>
        public Dictionary<string, Device> GetDevices(IEnumerable<string> procMountsContent)
        {
            var mountingPointToDevice = new Dictionary<string, Device>();
            foreach (string key in _keys)
            {
                mountingPointToDevice[key] = null;
            }

            foreach (string mountInfo in procMountsContent)
            {
                string[] items = mountInfo.Split(' '); // split the line into column values
                if (items.Length != 6)
                {
                    Trace.WriteLine($"Line {mountInfo} from /proc/mounts file is corrupted");
                    continue;
                }
                string mountingPoint = items[1]; // read mounting point of this line
                if (_keys.Contains(mountingPoint))
                {
                    // get device from the line and add it to the resulting dictionary
                    mountingPointToDevice[mountingPoint] = new Device(items[0], items[2]);
                }
            }

            return mountingPointToDevice;
        }
    }
}

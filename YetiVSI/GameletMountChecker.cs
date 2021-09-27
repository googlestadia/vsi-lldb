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
        // Default configuration, /mnt/developer is bind-mounted to /srv/game/assets.
        None = 0,

        // /mnt/developer is mounted as top-level overlay to /srv/game/assets.
        InstanceStorageOverlay = 1 << 0,

        // A package is mounted to /mnt/package.
        PackageMounted = 1 << 1,

        // A game was run directly, e.g. with ggp run --package.
        RunFromPackage = 1 << 2,

        // A local workstation directory is mounted to /mnt/workstation and
        // visible in /srv/game/assets, i.e. asset streaming is active.
        LocalDirMounted = 1 << 3,
    }

    /// <summary>
    /// Provides logic to verify gamelet mount state before running the application.
    /// </summary>
    public class GameletMountChecker
    {
        public const string ReadMountsCmd = "cat /proc/mounts";

        const string _overlayFileSystem = "overlay";

        // Possible values are be fuse.sshfs (AS20) or fuse.cdc_fuse_fs (AS30).
        const string _fuseFileSystem = "fuse.";

        readonly HashSet<string> _keys = new HashSet<string>
        {
            YetiConstants.GameAssetsMountingPoint,
            YetiConstants.PackageMountingPoint,
            YetiConstants.DeveloperMountingPoint,
            YetiConstants.WorkstationMountingPoint
        };

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
            Device workstationDevice = devices[YetiConstants.WorkstationMountingPoint];
            Device packageDevice = devices[YetiConstants.PackageMountingPoint];
            Device developerDevice = devices[YetiConstants.DeveloperMountingPoint];

            bool gameAssetsIsOverlay =
                gameAssetsDevice?.FileSystemType.Equals(_overlayFileSystem, _comparisonType) ??
                false;

            // Check whether /srv/game/assets is mounted as overlayfs with /mnt/developer as
            // mount dir. gameAssetsDevice.Parameters lists the overlay layers, e.g.
            // lowerdir=/home/cloudcast/.overlayfs_workaround_layer:/mnt/workstation:/mnt/package.
            if (gameAssetsIsOverlay &&
                gameAssetsDevice.Parameters.Contains(YetiConstants.DeveloperMountingPoint))
            {
                mountConfiguration |= MountConfiguration.InstanceStorageOverlay;
            }

            if (workstationDevice?.FileSystemType.StartsWith(_fuseFileSystem, _comparisonType) ??
                false)
            {
                if (gameAssetsIsOverlay &&
                    gameAssetsDevice.Parameters.Contains(YetiConstants.WorkstationMountingPoint))
                {
                    // Mounted as overlay, e.g. 'ggp instance mount --local-dir --package'.
                    mountConfiguration |= MountConfiguration.LocalDirMounted;
                }
                else if (gameAssetsDevice?.FileSystemType.StartsWith(
                    _fuseFileSystem, _comparisonType) ?? false)
                {
                    // Mounted as bind-mount, e.g. 'ggp instance mount --local-dir'.
                    mountConfiguration |= MountConfiguration.LocalDirMounted;
                }
            }

            if (!string.IsNullOrWhiteSpace(packageDevice?.Address))
            {
                if (gameAssetsIsOverlay &&
                    gameAssetsDevice.Parameters.Contains(YetiConstants.PackageMountingPoint))
                {
                    // Mounted as overlay, e.g. 'ggp instance mount --local-dir --package'.
                    mountConfiguration |= MountConfiguration.PackageMounted;
                }
                else if (packageDevice.Address.Equals(gameAssetsDevice?.Address, _comparisonType))
                {
                    // Mounted as bind-mount, e.g. 'ggp instance mount --package'.
                    mountConfiguration |= MountConfiguration.PackageMounted;
                }
            }

            if (gameAssetsDevice != null &&
                !gameAssetsDevice.Address.Equals(developerDevice?.Address, _comparisonType) &&
                !gameAssetsDevice.Address.Equals(workstationDevice?.Address, _comparisonType) &&
                !gameAssetsDevice.Address.Equals(packageDevice?.Address, _comparisonType) &&
                !gameAssetsIsOverlay)
            {
                // Game was run from package, e.g. 'ggp run --package'.
                mountConfiguration |= MountConfiguration.RunFromPackage;
            }

            return mountConfiguration;
        }

        /// <summary>
        /// Returns true iff writes to /mnt/developer are not visible in /srv/game/assets.
        /// </summary>
        /// <param name="currentMountConfiguration">
        /// Mount configuration as returned by GetConfiguration()
        /// </param>
        public bool IsGameAssetsDetachedFromDeveloperFolder(
            MountConfiguration currentMountConfiguration) =>
            currentMountConfiguration != MountConfiguration.None &&
            !currentMountConfiguration.HasFlag(MountConfiguration.InstanceStorageOverlay);

        List<string> ReadMountsContentOrDefault(Gamelet gamelet, ActionRecorder actionRecorder)
        {
            List<string> content = new List<string>();
            ICancelableTask getMountsTask = _cancelableTaskFactory.Create(
                TaskMessages.CheckingMountInfo,
                async _ =>
                {
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
            public Device(string address, string fileSystemType, string parameters)
            {
                Address = address;
                FileSystemType = fileSystemType;
                Parameters = parameters;
            }

            public string Address { get; }
            public string FileSystemType { get; }
            public string Parameters { get; }
        }

        /// <summary>
        /// Returns dictionary of the mounting points of interest (/srv/game/assets, /mnt/package,
        /// /mnt/developer) to the devices they are currently mounted to.
        /// Format of /proc/mounts is:
        /// 1st column - device that is mounted;
        /// 2nd column - the mount point;
        /// 3rd column - the file-system type;
        /// 4th column - file system parameters, e.g. read-only (ro), read-write (rw);
        /// 5th, 6th column - place-holders;
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
                // Split the line into column values.
                string[] items = mountInfo.Split(' ');
                if (items.Length != 6)
                {
                    Trace.WriteLine($"Line {mountInfo} from /proc/mounts file is corrupted");
                    continue;
                }

                // Read mounting point of this line.
                string mountingPoint = items[1];
                if (_keys.Contains(mountingPoint))
                {
                    // Get device from the line and add it to the resulting dictionary.
                    mountingPointToDevice[mountingPoint] = new Device(items[0], items[2], items[3]);
                }
            }

            return mountingPointToDevice;
        }
    }
}
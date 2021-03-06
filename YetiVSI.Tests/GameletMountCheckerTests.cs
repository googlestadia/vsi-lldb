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
using Microsoft.VisualStudio.Threading;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using YetiCommon;
using YetiCommon.SSH;
using YetiVSI.Metrics;
using YetiVSI.Shared.Metrics;
using YetiVSITestsCommon;

namespace YetiVSI.Test
{
    [TestFixture]
    class GameletMountCheckerTests
    {
        readonly Gamelet _gamelet = new Gamelet();
        readonly IDialogUtil _dialogUtil = Substitute.For<IDialogUtil>();
        IRemoteCommand _remoteCommand;
        CancelableTask.Factory _cancelableTaskFactory =
            FakeCancelableTask.CreateFactory(new JoinableTaskContext(), false);
        ActionRecorder _actionRecorder;

        readonly List<string> _cleanGamelet = new List<string> {
            "tmpfs /run/user/1000 tmpfs rw,nosuid,nodev,relatime,size=size_in_k,mode=700,uid=1000,gid=1000 0 0",
            "/dev/sde6 /mnt/developer ext4 rw,relatime 0 0",
            "/dev/sde6 /srv/game/assets ext4 ro,relatime,norecovery 0 0"
        };

        readonly List<string> _corruptedFile = new List<string> {
            "/dev/sde6 /mnt/developer",     // this won't be processed because fields are missing
            "/dev/sde6 /srv/game/assets 2", // this won't be processed because fields are missing,
            "/dev/sde6 /srv/game/assets 2 a d" // this won't be processed because of extra fields
        };

        readonly List<string> _mountedWithNoOverlay = new List<string> {
            "tmpfs /run/user/1000 tmpfs rw,nosuid,nodev,relatime,size=size_in_k,mode=700,uid=1000,gid=1000 0 0",
            "/dev/sde6 /mnt/developer ext4 rw,relatime 0 0",
            "/dev/mapper/cryptfs-disk-0129389243020 /mnt/package ext4 ro,relatime,norecovery 0 0",
            "/dev/mapper/cryptfs-disk-0129389243020 /srv/game/assets ext4 ro,relatime,norecovery 0 0"
        };

        readonly List<string> _mountedWithOverlay = new List<string> {
            "/dev/sde4 /mnt/localssd/saves ext4 rw,relatime 0 0",
            "/dev/sde6 /mnt/developer ext4 rw,relatime 0 0",
            "/dev/mapper/cryptfs-disk-0129389243020 /mnt/package ext4 ro,relatime,norecovery 0 0",
            "overlay /srv/game/assets overlay ro,relatime,lowerdir=/mnt/developer:/mnt/package 0 0"
        };

        readonly List<string> _packageRanDirectly = new List<string> {
            "tmpfs /run/user/0 tmpfs rw,nosuid,nodev,relatime,size=size_in_k,mode=700 0 0",
            "tmpfs /run/user/1000 tmpfs rw,nosuid,nodev,relatime,size=size_in_k,mode=700,uid=1000,gid=1000 0 0",
            "/dev/sde6 /mnt/developer ext4 rw,relatime 0 0",
            "/dev/mapper/cryptfs-disk-0129389243020 /srv/game/assets ext4 ro,relatime,norecovery 0 0"
        };

        readonly List<string> _localDirStreaming = new List<string> {
            "machine@localhost:/ /mnt/workstation fuse.sshfs rw,nosuid,nodev,relatime,user_id=1000,group_id=1000,allow_other 0 0",
            "machine@localhost:/ /mnt/developer fuse.sshfs rw,nosuid,nodev,relatime,user_id=1000,group_id=1000,allow_other 0 0",
            "machine@localhost:/ /srv/game/assets fuse.sshfs ro,relatime,user_id=1000,group_id=1000,allow_other 0 0",
        };

        readonly List<string> _localDirStreamingAsOverlay = new List<string> {
            "machine@localhost:/ /mnt/workstation fuse.sshfs rw,nosuid,nodev,relatime,user_id=1000,group_id=1000,allow_other 0 0",
            "machine@localhost:/ /mnt/developer fuse.sshfs rw,nosuid,nodev,relatime,user_id=1000,group_id=1000,allow_other 0 0",
            "/dev/mapper/cryptfs-disk-0129389243020 /mnt/package ext4 ro,relatime,norecovery 0 0",
            "overlay /srv/game/assets overlay ro,relatime,lowerdir=/home/cloudcast/.overlayfs_workaround_layer:/mnt/developer:/mnt/package 0 0"
        };

        [SetUp]
        public void SetUp()
        {
            _remoteCommand = Substitute.For<IRemoteCommand>();
            _dialogUtil.ShowYesNo(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
            _actionRecorder = new ActionRecorder(Substitute.For<IMetrics>());
        }

        [Test]
        public void GetConfigurationForEmptyFileReturnsNone()
        {
            var emptyContent = new List<string>();
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(emptyContent);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(configuration, Is.EqualTo(MountConfiguration.None));
        }

        [Test]
        public void GetConfigurationForCleanGameletReturnsNone()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_cleanGamelet);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(configuration, Is.EqualTo(MountConfiguration.None));
        }

        [Test]
        public void GetConfigurationForMountedWithNoOverlayReturnsCorrectCombination()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_mountedWithNoOverlay);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(configuration, Is.EqualTo(MountConfiguration.PackageMounted));
        }

        [Test]
        public void GetConfigurationForMountedWithOverlayReturnsCorrectCombination()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_mountedWithOverlay);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(configuration,
                        Is.EqualTo(MountConfiguration.PackageMounted | MountConfiguration.Overlay));
        }

        [Test]
        public void GetConfigurationForPackageRanDirectlyReturnsCorrectCombination()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_packageRanDirectly);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(configuration, Is.EqualTo(MountConfiguration.RunFromPackage));
        }

        [Test]
        public void GetConfigurationForAssetStreamingReturnsCorrectCombination()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_localDirStreaming);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(configuration, Is.EqualTo(MountConfiguration.LocalStreaming));
        }

        [Test]
        public void GetConfigurationForMountedWithLocalFolderAsOverlayReturnsCorrectCombination()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_localDirStreamingAsOverlay);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(configuration,
                        Is.EqualTo(MountConfiguration.PackageMounted | MountConfiguration.Overlay |
                                   MountConfiguration.LocalStreaming));
        }

        [Test]
        public void GetConfigurationForCorruptedFileReturnsNone()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_corruptedFile);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(configuration, Is.EqualTo(MountConfiguration.None));
        }

        [Test]
        public void GetDevicesWhenReadingFailedReturnsEmpty()
        {
            var mountChecker = new GameletMountChecker(null, null, null);
            var emptyContent = new List<string>();
            Dictionary<string, GameletMountChecker.Device> devices =
                mountChecker.GetDevices(emptyContent);
            Assert.Multiple(() => {
                Assert.That(devices[YetiConstants.PackageMountingPoint]?.Address, Is.EqualTo(null));
                Assert.That(devices[YetiConstants.GameAssetsMountingPoint]?.Address,
                            Is.EqualTo(null));
                Assert.That(devices[YetiConstants.DeveloperMountingPoint]?.Address,
                            Is.EqualTo(null));
            });
        }

        [Test]
        public void GetDevicesOnCleanGameletHasGameAssetsAndDeveloperMountSet()
        {
            var mountChecker = new GameletMountChecker(null, null, null);
            Dictionary<string, GameletMountChecker.Device> devices =
                mountChecker.GetDevices(_cleanGamelet);
            Assert.Multiple(() => {
                Assert.That(devices[YetiConstants.PackageMountingPoint]?.Address, Is.EqualTo(null));
                Assert.That(devices[YetiConstants.GameAssetsMountingPoint]?.Address,
                            Is.EqualTo("/dev/sde6"));
                Assert.That(devices[YetiConstants.DeveloperMountingPoint]?.Address,
                            Is.EqualTo("/dev/sde6"));
            });
        }

        [Test]
        public void GetDevicesOnGameletWithMountedPackageWithoutOverlay()
        {
            var mountChecker = new GameletMountChecker(null, null, null);
            Dictionary<string, GameletMountChecker.Device> devices =
                mountChecker.GetDevices(_mountedWithNoOverlay);
            Assert.Multiple(() => {
                Assert.That(devices[YetiConstants.PackageMountingPoint]?.Address,
                            Is.EqualTo("/dev/mapper/cryptfs-disk-0129389243020"));
                Assert.That(devices[YetiConstants.GameAssetsMountingPoint]?.Address,
                            Is.EqualTo("/dev/mapper/cryptfs-disk-0129389243020"));
                Assert.That(devices[YetiConstants.DeveloperMountingPoint]?.Address,
                            Is.EqualTo("/dev/sde6"));
            });
        }

        [Test]
        public void GetDevicesOnGameletWithMountedPackageWithOverlay()
        {
            var mountChecker = new GameletMountChecker(null, null, null);
            Dictionary<string, GameletMountChecker.Device> devices =
                mountChecker.GetDevices(_mountedWithOverlay);
            Assert.Multiple(() => {
                Assert.That(devices[YetiConstants.PackageMountingPoint]?.Address,
                            Is.EqualTo("/dev/mapper/cryptfs-disk-0129389243020"));
                Assert.That(devices[YetiConstants.GameAssetsMountingPoint]?.Address,
                            Is.EqualTo("overlay"));
                Assert.That(devices[YetiConstants.DeveloperMountingPoint]?.Address,
                            Is.EqualTo("/dev/sde6"));
            });
        }

        [Test]
        public void GetDevicesOnGameletAfterPackageRunHasGameAssetsAndDeveloperMountSet()
        {
            var mountChecker = new GameletMountChecker(null, null, null);
            Dictionary<string, GameletMountChecker.Device> devices =
                mountChecker.GetDevices(_packageRanDirectly);
            Assert.Multiple(() => {
                Assert.That(devices[YetiConstants.PackageMountingPoint]?.Address, Is.EqualTo(null));
                Assert.That(devices[YetiConstants.GameAssetsMountingPoint]?.Address,
                            Is.EqualTo("/dev/mapper/cryptfs-disk-0129389243020"));
                Assert.That(devices[YetiConstants.DeveloperMountingPoint]?.Address,
                            Is.EqualTo("/dev/sde6"));
            });
        }

        [Test]
        public void GetDevicesOnCorruptedFileReturnsEmpty()
        {
            var mountChecker = new GameletMountChecker(null, null, null);
            Dictionary<string, GameletMountChecker.Device> devices =
                mountChecker.GetDevices(_corruptedFile);
            Assert.Multiple(() => {
                Assert.That(devices[YetiConstants.PackageMountingPoint]?.Address, Is.EqualTo(null));
                Assert.That(devices[YetiConstants.GameAssetsMountingPoint]?.Address,
                            Is.EqualTo(null));
                Assert.That(devices[YetiConstants.DeveloperMountingPoint]?.Address,
                            Is.EqualTo(null));
            });
        }

        [Test]
        public void IsGameAssetsDetachedFromDeveloperFolderForMountWithOverlayIsFalse()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_mountedWithOverlay);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(mountChecker.IsGameAssetsDetachedFromDeveloperFolder(configuration),
                        Is.False);
        }

        [Test]
        public void IsGameAssetsDetachedFromDeveloperFolderForMountWithoutOverlayIsTrue()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_mountedWithNoOverlay);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(mountChecker.IsGameAssetsDetachedFromDeveloperFolder(configuration),
                        Is.True);
        }

        [Test]
        public void IsGameAssetsDetachedFromDeveloperFolderAfterPackageRunIsTrue()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_packageRanDirectly);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(mountChecker.IsGameAssetsDetachedFromDeveloperFolder(configuration),
                        Is.True);
        }

        [Test]
        public void IsGameAssetsDetachedFromDeveloperFolderForCleanStateIsFalse()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_cleanGamelet);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(mountChecker.IsGameAssetsDetachedFromDeveloperFolder(configuration),
                        Is.False);
        }

        [Test]
        public void IsGameAssetsDetachedFromDeveloperFolderForStreamingAssetsIsFalse()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_localDirStreaming);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(mountChecker.IsGameAssetsDetachedFromDeveloperFolder(configuration),
                        Is.False);
        }

        [Test]
        public void IsGameAssetsDetachedFromDeveloperFolderForPackageWithLocalDirAsOverlayIsFalse()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_localDirStreamingAsOverlay);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);
            Assert.That(mountChecker.IsGameAssetsDetachedFromDeveloperFolder(configuration),
                        Is.False);
        }

        [Test]
        public void TestCheckMountConfigurationWhenFailedSshIsFalseAndWarningShowed()
        {
            _remoteCommand
                .When(c => c.RunWithSuccessCapturingOutputAsync(Arg.Any<SshTarget>(),
                                                                Arg.Any<string>()))
                .Do(_ => throw new ProcessExecutionException("oops", 1));

            var mountChecker =
                new GameletMountChecker(_remoteCommand, _dialogUtil, _cancelableTaskFactory);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);
            bool result = mountChecker.IsGameAssetsDetachedFromDeveloperFolder(configuration);

            Assert.That(result, Is.False);
            _dialogUtil.Received(1).ShowError(Arg.Any<string>(), Arg.Any<string>());
        }

        GameletMountChecker CreateMountCheckerWithSpecifiedProcMountsInfo(
            List<string> procMountsContent)
        {
            _remoteCommand
                .RunWithSuccessCapturingOutputAsync(new SshTarget(_gamelet),
                                                    GameletMountChecker.ReadMountsCmd)
                .Returns(Task.FromResult(procMountsContent));
            return new GameletMountChecker(_remoteCommand, _dialogUtil, _cancelableTaskFactory);
        }
    }
}

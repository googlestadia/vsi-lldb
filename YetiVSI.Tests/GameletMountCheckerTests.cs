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

        // ggp instance unmount
        readonly List<string> _unmounted = new List<string>
        {
            "/dev/sde6 /srv/game/assets ext4 ro,relatime 0 0",
            "/dev/sde6 /mnt/developer ext4 rw,relatime 0 0",
            "tmpfs /run/user/1000 tmpfs rw,nosuid,nodev,relatime,size=1048576k,mode=700,uid=1000,gid=1000,inode64 0 0"
        };

        readonly List<string> _corruptedProcMountFile = new List<string>
        {
            "/dev/sde6 /mnt/developer", // this won't be processed because fields are missing
            "/dev/sde6 /srv/game/assets ext4 ro", // this won't be processed because fields are missing,
            "/dev/sde6 /srv/game/assets ext4 ro 0 0 0" // this won't be processed because of extra fields
        };

        // ggp instance mount --package
        readonly List<string> _mountedPackage = new List<string>
        {
            "/dev/sde6 /mnt/developer ext4 rw,relatime 0 0",
            "/dev/mapper/cryptfs-disk-0129389243020 /mnt/package ext4 ro,relatime,norecovery 0 0",
            "/dev/mapper/cryptfs-disk-0129389243020 /srv/game/assets ext4 ro,relatime,norecovery 0 0",
            "tmpfs /run/user/1000 tmpfs rw,nosuid,nodev,relatime,size=1048576k,mode=700,uid=1000,gid=1000,inode64 0 0"
        };

        // ggp instance mount --package --overlay-instance-storage
        readonly List<string> _mountedPackageWithOverlay = new List<string>
        {
            "/dev/sde6 /mnt/developer ext4 rw,relatime 0 0",
            "/dev/mapper/cryptfs-disk-0129389243020 /mnt/package ext4 ro,relatime,norecovery 0 0",
            "overlay /srv/game/assets overlay ro,relatime,lowerdir=/mnt/developer:/mnt/package 0 0",
            "tmpfs /run/user/1000 tmpfs rw,nosuid,nodev,relatime,size=1048576k,mode=700,uid=1000,gid=1000,inode64 0 0"
        };

        // ggp instance mount --local-dir with Asset Streaming 2.0
        readonly List<string> _mountedLocalDirAS20 = new List<string>
        {
            "/dev/sde6 /mnt/developer ext4 rw,relatime 0 0",
            "machine@localhost:/ /mnt/workstation fuse.sshfs rw,nosuid,nodev,relatime,user_id=1000,group_id=1000,allow_other 0 0",
            "machine@localhost:/ /srv/game/assets fuse.sshfs ro,relatime,user_id=1000,group_id=1000,allow_other 0 0",
            "tmpfs /run/user/1000 tmpfs rw,nosuid,nodev,relatime,size=1048576k,mode=700,uid=1000,gid=1000,inode64 0 0"
        };

        // ggp instance mount --local-dir with Asset Streaming 3.0
        readonly List<string> _mountedLocalDirAS30 = new List<string>
        {
            "/dev/sde6 /mnt/developer ext4 rw,relatime 0 0",
            "cdc_fuse_fs /mnt/workstation fuse.cdc_fuse_fs ro,nosuid,nodev,relatime,user_id=1000,group_id=1000,allow_other 0 0",
            "cdc_fuse_fs /srv/game/assets fuse.cdc_fuse_fs ro,relatime,user_id=1000,group_id=1000,allow_other 0 0",
            "tmpfs /run/user/1000 tmpfs rw,nosuid,nodev,relatime,size=1048576k,mode=700,uid=1000,gid=1000,inode64 0 0"
        };

        // ggp instance mount --local-dir --overlay-instance-storage
        readonly List<string> _mountedLocalDirWithOverlay = new List<string>
        {
            "/dev/sde6 /mnt/developer ext4 rw,relatime 0 0",
            "cdc_fuse_fs /mnt/workstation fuse.cdc_fuse_fs ro,nosuid,nodev,relatime,user_id=1000,group_id=1000,allow_other 0 0",
            "overlay /srv/game/assets overlay ro,relatime,lowerdir=/mnt/developer:/mnt/workstation 0 0",
            "tmpfs /run/user/1000 tmpfs rw,nosuid,nodev,relatime,size=1048576k,mode=700,uid=1000,gid=1000,inode64 0 0"
        };

        // ggp instance mount --package --local-dir
        readonly List<string> _mountedPackageAndLocalDir = new List<string>
        {
            "/dev/sde6 /mnt/developer ext4 rw,relatime 0 0",
            "cdc_fuse_fs /mnt/workstation fuse.cdc_fuse_fs ro,nosuid,nodev,relatime,user_id=1000,group_id=1000,allow_other 0 0",
            "/dev/mapper/cryptfs-disk-0129389243020 /mnt/package ext4 ro,relatime,norecovery 0 0",
            "overlay /srv/game/assets overlay ro,relatime,lowerdir=/home/cloudcast/.overlayfs_workaround_layer:/mnt/workstation:/mnt/package 0 0",
            "tmpfs /run/user/1000 tmpfs rw,nosuid,nodev,relatime,size=1048576k,mode=700,uid=1000,gid=1000,inode64 0 0"
        };

        // ggp instance mount --package --local-dir --instance-storage-overlay
        readonly List<string> _mountedPackageAndLocalDirWithOverlay = new List<string>
        {
            "/dev/sde6 /mnt/developer ext4 rw,relatime 0 0",
            "cdc_fuse_fs /mnt/workstation fuse.cdc_fuse_fs ro,nosuid,nodev,relatime,user_id=1000,group_id=1000,allow_other 0 0",
            "/dev/mapper/cryptfs-disk-0129389243020 /mnt/package ext4 ro,relatime,norecovery 0 0",
            "overlay /srv/game/assets overlay ro,relatime,lowerdir=/mnt/developer:/mnt/workstation:/mnt/package 0 0",
            "tmpfs /run/user/1000 tmpfs rw,nosuid,nodev,relatime,size=1048576k,mode=700,uid=1000,gid=1000,inode64 0 0"
        };

        // ggp run --package
        readonly List<string> _runFromPackage = new List<string>
        {
            "/dev/sde6 /mnt/developer ext4 rw,relatime 0 0",
            "/dev/mapper/cryptfs-content-asset-1-ad29163532d94905 /srv/game/assets ext4 ro,relatime,norecovery 0 0",
            "tmpfs /run/user/1000 tmpfs rw,nosuid,nodev,relatime,size=1048576k,mode=700,uid=1000,gid=1000,inode64 0 0"
        };

        // ggp instance mount --local-dir && ggp run --package
        readonly List<string> _runFromPackageWithLeftOverFuse = new List<string>
        {
            "cdc_fuse_fs /mnt/workstation fuse.cdc_fuse_fs ro,nosuid,nodev,relatime,user_id=1000,group_id=1000,allow_other 0 0",
            "/dev/sde6 /mnt/developer ext4 rw,relatime 0 0",
            "/dev/mapper/cryptfs-content-asset-1-ad29163532d94905 /srv/game/assets ext4 ro,relatime,norecovery 0 0",
            "tmpfs /run/user/1000 tmpfs rw,nosuid,nodev,relatime,size=1048576k,mode=700,uid=1000,gid=1000,inode64 0 0"
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
        public void GetConfigurationForUnmountedGameletReturnsNone()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_unmounted);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(configuration, Is.EqualTo(MountConfiguration.None));
        }

        [Test]
        public void GetConfigurationForMountedPackage()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_mountedPackage);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(configuration, Is.EqualTo(MountConfiguration.PackageMounted));
        }

        [Test]
        public void GetConfigurationForMountedPackageWithOverlay()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_mountedPackageWithOverlay);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(configuration,
                        Is.EqualTo(MountConfiguration.PackageMounted |
                                   MountConfiguration.InstanceStorageOverlay));
        }

        [Test]
        public void GetConfigurationForMountedLocalDirAS20()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_mountedLocalDirAS20);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(configuration, Is.EqualTo(MountConfiguration.LocalDirMounted));
        }

        [Test]
        public void GetConfigurationForMountedLocalDirAS30()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_mountedLocalDirAS30);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(configuration, Is.EqualTo(MountConfiguration.LocalDirMounted));
        }

        [Test]
        public void GetConfigurationForMountedLocalDirWithOverlay()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_mountedLocalDirWithOverlay);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(configuration,
                        Is.EqualTo(MountConfiguration.InstanceStorageOverlay |
                                   MountConfiguration.LocalDirMounted));
        }

        [Test]
        public void GetConfigurationForMountedPackageAndLocalDir()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_mountedPackageAndLocalDir);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(configuration,
                        Is.EqualTo(MountConfiguration.PackageMounted |
                                   MountConfiguration.LocalDirMounted));
        }

        [Test]
        public void GetConfigurationForMountedPackageAndLocalDirWithOverlay()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(
                    _mountedPackageAndLocalDirWithOverlay);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(configuration,
                        Is.EqualTo(MountConfiguration.InstanceStorageOverlay |
                                   MountConfiguration.PackageMounted |
                                   MountConfiguration.LocalDirMounted));
        }

        [Test]
        public void GetConfigurationForRunFromPackage()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_runFromPackage);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(configuration, Is.EqualTo(MountConfiguration.RunFromPackage));
        }

        [Test]
        public void GetConfigurationForRunFromPackageWithLeftOverFuse()
        {
            // The FUSE mounted to /mnt/workstation should be ignored since it is not visible
            // in /srv/game/assets.
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_runFromPackageWithLeftOverFuse);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(configuration, Is.EqualTo(MountConfiguration.RunFromPackage));
        }

        [Test]
        public void GetConfigurationForCorruptedFileReturnsNone()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_corruptedProcMountFile);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(configuration, Is.EqualTo(MountConfiguration.None));
        }

        [Test]
        public void IsGameAssetsDetachedFromDeveloperFolderForMountWithOverlayIsFalse()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_mountedPackageWithOverlay);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(mountChecker.IsGameAssetsDetachedFromDeveloperFolder(configuration),
                        Is.False);
        }

        [Test]
        public void IsGameAssetsDetachedFromDeveloperFolderForMountWithoutOverlayIsTrue()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_mountedPackage);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(mountChecker.IsGameAssetsDetachedFromDeveloperFolder(configuration),
                        Is.True);
        }

        [Test]
        public void IsGameAssetsDetachedFromDeveloperFolderForMountedPackageIsTrue()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_mountedPackage);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(mountChecker.IsGameAssetsDetachedFromDeveloperFolder(configuration),
                        Is.True);
        }

        [Test]
        public void IsGameAssetsDetachedFromDeveloperFolderForMountedLocalDirIsTrue()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_mountedLocalDirAS30);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(mountChecker.IsGameAssetsDetachedFromDeveloperFolder(configuration),
                        Is.True);
        }

        [Test]
        public void IsGameAssetsDetachedFromDeveloperFolderForMountedPackageAndLocalDirIsTrue()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_mountedPackageAndLocalDir);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(mountChecker.IsGameAssetsDetachedFromDeveloperFolder(configuration),
                        Is.True);
        }

        [Test]
        public void IsGameAssetsDetachedFromDeveloperFolderForUnmountedStateIsFalse()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_unmounted);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(mountChecker.IsGameAssetsDetachedFromDeveloperFolder(configuration),
                        Is.False);
        }

        [Test]
        public void IsGameAssetsDetachedFromDeveloperFolderForMountedPackageWithOverlayIsFalse()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_mountedPackageWithOverlay);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);

            Assert.That(mountChecker.IsGameAssetsDetachedFromDeveloperFolder(configuration),
                        Is.False);
        }

        [Test]
        public void IsGameAssetsDetachedFromDeveloperFolderForMountedLocalDirWithOverlayIsFalse()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(_mountedLocalDirWithOverlay);

            MountConfiguration configuration =
                mountChecker.GetConfiguration(_gamelet, _actionRecorder);
            Assert.That(mountChecker.IsGameAssetsDetachedFromDeveloperFolder(configuration),
                        Is.False);
        }

        [Test]
        public void
            IsGameAssetsDetachedFromDeveloperFolderForMountedPackageAndLocalDirWithOverlayIsFalse()
        {
            GameletMountChecker mountChecker =
                CreateMountCheckerWithSpecifiedProcMountsInfo(
                    _mountedPackageAndLocalDirWithOverlay);

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
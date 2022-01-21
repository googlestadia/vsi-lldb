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

using GgpGrpc.Cloud;
using GgpGrpc.Models;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using YetiCommon;
using YetiCommon.Cloud;
using YetiCommon.SSH;
using YetiVSI.Metrics;
using YetiVSI.Shared.Metrics;
using YetiVSI.Util;

namespace YetiVSI.CoreAttach
{
    // A window showing a list of crash dumps that the user can attach to.
    public partial class CoreAttachWindow : DialogWindow
    {
        readonly JoinableTaskContext _taskContext;
        readonly CancelableTask.Factory _cancelableTaskFactory;
        readonly ProjectInstanceSelection.Factory _instanceSelectionWindowFactory;
        readonly ICoreListRequest _coreListRequest;
        readonly ICloudRunner _cloudRunner;
        readonly GameletClient.Factory _gameletClientFactory;
        readonly IRemoteFile _remoteFile;
        readonly ISshManager _sshManager;
        readonly IDialogUtil _dialogUtil;
        readonly ActionRecorder _actionRecorder;
        readonly DebugSessionMetrics _debugSessionMetrics;
        readonly string _developerAccount;
        Gamelet _instance;

        public CoreAttachWindow(IServiceProvider serviceProvider)
        {
            try
            {
                var serviceManager = new ServiceManager();
                _taskContext = serviceManager.GetJoinableTaskContext();

                _dialogUtil = new DialogUtil();
                IExtensionOptions options =
                    ((YetiVSIService)serviceManager.RequireGlobalService(typeof(YetiVSIService)))
                    .Options;
                var managedProcessFactory = new ManagedProcess.Factory();
                var progressDialogFactory = new ProgressDialog.Factory();
                _cancelableTaskFactory =
                    new CancelableTask.Factory(_taskContext, progressDialogFactory);
                _coreListRequest = new CoreListRequest.Factory().Create();
                var jsonUtil = new JsonUtil();
                var credentialConfigFactory = new CredentialConfig.Factory(jsonUtil);
                var accountOptionLoader = new VsiAccountOptionLoader(options);
                var credentialManager =
                    new CredentialManager(credentialConfigFactory, accountOptionLoader);
                _developerAccount = credentialManager.LoadAccount();
                IRemoteCommand remoteCommand = new RemoteCommand(managedProcessFactory);
                _remoteFile = new RemoteFile(managedProcessFactory);
                var cloudConnection = new CloudConnection();
                var sdkConfigFactory = new SdkConfig.Factory(jsonUtil);
                // NOTE: the lifetime of this CloudRunner is limited to the current CoreAttachWindow.
                _cloudRunner = new CloudRunner(sdkConfigFactory, credentialManager, cloudConnection,
                                               new GgpSDKUtil());
                _gameletClientFactory = new GameletClient.Factory();
                var sshKeyLoader = new SshKeyLoader(managedProcessFactory);
                var sshKnownHostsWriter = new SshKnownHostsWriter();
                _sshManager = new SshManager(_gameletClientFactory, _cloudRunner, sshKeyLoader,
                                             sshKnownHostsWriter, remoteCommand);
                _debugSessionMetrics = new DebugSessionMetrics(
                    serviceProvider.GetService(typeof(SMetrics)) as IMetrics);
                _debugSessionMetrics.UseNewDebugSessionId();
                _actionRecorder = new ActionRecorder(_debugSessionMetrics);

                InitializeComponent();
                _instanceSelectionWindowFactory = new ProjectInstanceSelection.Factory();
                SelectInstanceOnInit();
            }
            catch (Exception exception)
            {
                Trace.WriteLine($"CoreAttachWindow ctor failed: {exception.Demystify()}");
                throw;
            }
        }

        void SelectInstanceOnInit()
        {
            List<Gamelet> instances = ListInstances();
            if (instances == null)
            {
                return;
            }

            var ownedInstances = FilterOwnedInstances(instances);
            SelectInstanceLabel.Content = $"Select from {instances.Count} Project Instances " +
                $"({ownedInstances.Count} reserved):";
            if (ownedInstances.Count == 1)
            {
                SetCurrentInstance(ownedInstances[0]);
            }
        }

        void SelectInstance()
        {
            List<Gamelet> instances = ListInstances();
            if (instances == null)
            {
                return;
            }

            SortInstancesByReserver(instances);

            var selected = _instanceSelectionWindowFactory.Create(instances).Run();
            // The operation was canceled.
            if (selected == null)
            {
                return;
            }

            SetCurrentInstance(selected);
        }

        void SetCurrentInstance(Gamelet instance)
        {
            _instance = instance;
            if (!EnableSsh())
            {
                _instance = null;
            }
            GameletMessageTextBox.Text = "";
            RefreshInstanceLabel();
            RefreshCoreList();
        }

        void RefreshCoreList()
        {
            if (_instance == null)
            {
                CoreList.ItemsSource = new List<CoreListEntry>();
                return;
            }

            Cursor = System.Windows.Input.Cursors.Wait;
            try
            {
                var queryTaskTitle = "Querying instance crash dumps...";
                var queryTask = _cancelableTaskFactory.Create(
                    queryTaskTitle,
                    async () => await _coreListRequest.GetCoreListAsync(new SshTarget(_instance)));

                // Ignore cancelation, and accept the empty result.
                queryTask.RunAndRecord(_actionRecorder, ActionType.CrashDumpList);
                CoreList.ItemsSource = queryTask.Result;
            }
            catch (ProcessException e)
            {
                Trace.WriteLine($"Unable to query instance crash dumps: {e.Demystify()}");
                GameletMessageTextBox.Text = ErrorStrings.ErrorQueryingCoreFiles(e.Message);
                CoreList.ItemsSource = new List<CoreListEntry>();
            }
            finally
            {
                Cursor = null;
            }
        }

        List<Gamelet> ListInstances()
        {
            try
            {
                var action = _actionRecorder.CreateToolAction(ActionType.GameletsList);
                var gameletClient = _gameletClientFactory.Create(_cloudRunner.Intercept(action));
                var queryTask = _cancelableTaskFactory.Create(
                    "Querying instances...",
                    async () => await gameletClient.ListGameletsAsync(false));
                if (!queryTask.RunAndRecord(action))
                {
                    return null;
                }

                return queryTask.Result;
            }
            catch (CloudException e)
            {
                Trace.Write("An exception was thrown while querying instances." +
                            Environment.NewLine + e);
                GameletMessageTextBox.Text = ErrorStrings.FailedToRetrieveGamelets(e.Message);
                return null;
            }
        }

        List<Gamelet> FilterOwnedInstances(List<Gamelet> instances) =>
            instances.Where(i => i.ReserverEmail == _developerAccount).ToList();

        void SortInstancesByReserver(List<Gamelet> instances)
        {
            instances.Sort((g1, g2) =>
            {
                if (g1.ReserverEmail != _developerAccount && g2.ReserverEmail != _developerAccount)
                {
                    return string.CompareOrdinal(g1.ReserverEmail, g2.ReserverEmail);
                }

                if (g1.ReserverEmail == _developerAccount && g2.ReserverEmail == _developerAccount)
                {
                    return string.CompareOrdinal(g1.DisplayName, g2.DisplayName);
                }

                return g1.ReserverEmail == _developerAccount ? -1 : 1;
            });
        }

        bool EnableSsh()
        {
            try
            {
                var action = _actionRecorder.CreateToolAction(ActionType.GameletEnableSsh);
                var enableSshTask = _cancelableTaskFactory.Create(
                    TaskMessages.EnablingSSH,
                    async _ => { await _sshManager.EnableSshAsync(_instance, action); });
                if (!enableSshTask.RunAndRecord(action))
                {
                    return false;
                }
            }
            catch (Exception e) when (e is CloudException || e is SshKeyException)
            {
                Trace.Write("An exception was thrown while enabling ssh." + Environment.NewLine +
                            e);
                GameletMessageTextBox.Text = ErrorStrings.FailedToEnableSsh(e.Message);
                return false;
            }

            return true;
        }
        void RefreshInstanceLabel()
        {
            InstanceLabel.Content = "Instance: " + (string.IsNullOrEmpty(_instance?.DisplayName)
                                                        ? _instance?.Id
                                                        : _instance.DisplayName);
        }

        void CoreListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AttachButton.IsEnabled = CoreList.SelectedItem != null;
        }

        void RefreshClick(object sender, RoutedEventArgs e)
        {
            RefreshCoreList();
        }

        void CancelClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        void AttachClick(object sender, RoutedEventArgs e)
        {
            _taskContext.ThrowIfNotOnMainThread();

            // Figure out core file path
            CoreListEntry? coreListEntry = null;
            if (TabControl.SelectedContent == GameletGroupBox)
            {
                coreListEntry = (CoreListEntry?) CoreList.SelectedItem;
                if (coreListEntry == null)
                {
                    _dialogUtil.ShowError(ErrorStrings.NoCoreFileSelected);
                    return;
                }
            }

            string coreFilePath = null;
            bool deleteAfter = false;
            if (TabControl.SelectedContent == LocalGroupBox)
            {
                coreFilePath = LocalCorePathBox.Text;
            }
            else if (TabControl.SelectedContent == GameletGroupBox)
            {
                var tempPath = Path.GetTempPath();
                try
                {
                    _cancelableTaskFactory
                        .Create(TaskMessages.DownloadingCoreFile,
                                async task => await _remoteFile.GetAsync(new SshTarget(_instance),
                                                                         "/usr/local/cloudcast/core/" +
                                                                         coreListEntry?.Name,
                                                                         tempPath, task))
                        .RunAndRecord(_actionRecorder, ActionType.CrashDumpDownload);
                }
                catch (ProcessException ex)
                {
                    Trace.WriteLine($"Failed to download core file: {ex.Demystify()}");
                    _dialogUtil.ShowError(ErrorStrings.FailedToDownloadCore(ex.Message), ex);
                    return;
                }

                coreFilePath = Path.Combine(tempPath, coreListEntry?.Name);
                deleteAfter = true;
            }

            if (string.IsNullOrEmpty(coreFilePath))
            {
                ShowMessage(ErrorStrings.FailedToRetrieveCoreFilePath);
                return;
            }

            // Check if we have a debugger (should always be the case).
            var vsDebugger =
                (IVsDebugger4) ServiceProvider.GlobalProvider.GetService(typeof(IVsDebugger));
            if (vsDebugger == null)
            {
                ShowMessage(ErrorStrings.FailedToStartDebugger);
                return;
            }

            try
            {
                _actionRecorder.RecordToolAction(ActionType.CrashDumpAttach, delegate
                {
                    _taskContext.ThrowIfNotOnMainThread();

                    VsDebugTargetInfo4[] debugTargets = new VsDebugTargetInfo4[1];
                    debugTargets[0].dlo = (uint) DEBUG_LAUNCH_OPERATION.DLO_CreateProcess;

                    // LaunchDebugTargets4() throws an exception if |bstrExe| and |bstrCurDir|
                    // are empty. Use core path and temp directory as placeholders.
                    debugTargets[0].bstrExe = coreFilePath;
                    debugTargets[0].bstrCurDir = Path.GetTempPath();

                    var parameters = new DebugEngine.DebugEngine.Params
                    {
                        CoreFilePath = coreFilePath,
                        DebugSessionId = _debugSessionMetrics.DebugSessionId,
                        DeleteCoreFile = deleteAfter
                    };
                    debugTargets[0].bstrOptions = JsonConvert.SerializeObject(parameters);
                    debugTargets[0].guidLaunchDebugEngine = YetiConstants.DebugEngineGuid;

                    var processInfo = new VsDebugTargetProcessInfo[debugTargets.Length];
                    vsDebugger.LaunchDebugTargets4(1, debugTargets, processInfo);
                });
            }
            catch (COMException ex)
            {
                Trace.WriteLine($"Failed to start debugger: {ex.Demystify()}");

                // Both DebugEngine and Visual Studio already show error dialogs if DebugEngine
                // has to abort while it's attaching, no need to show another dialog in that case.
                if (ex.ErrorCode != VSConstants.E_ABORT)
                {
                    _dialogUtil.ShowError(ErrorStrings.FailedToStartDebugger, ex);
                }
            }
            finally
            {
                Close();
            }
        }

        void BrowseClick(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Core files (*.core, *.dmp)|*.core; *.dmp|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                LocalCorePathBox.Text = openFileDialog.FileName;
            }
        }

        void TabSelected(object sender, SelectionChangedEventArgs e)
        {
            // |e| has the newly selected tab while |tabControl.SelectedContent| has the
            // previously selected content.
            if (e.AddedItems.Count == 0)
            {
                return;
            }

            GameletMessageTextBox.Text = "";
            LocalMessageTextBox.Text = "";

            if (e.AddedItems[0] == LocalTab)
            {
                AttachButton.IsEnabled = File.Exists(LocalCorePathBox.Text);
            }
        }

        void localCorePathBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            AttachButton.IsEnabled = File.Exists(LocalCorePathBox.Text);
            if (!string.IsNullOrEmpty(LocalCorePathBox.Text) && !File.Exists(LocalCorePathBox.Text))
            {
                LocalMessageTextBox.Text = ErrorStrings.CoreFileDoesNotExist;
            }
        }

        void ShowMessage(string message)
        {
            if (TabControl.SelectedContent == LocalGroupBox)
            {
                LocalMessageTextBox.Text = message;
            }
            else
            {
                GameletMessageTextBox.Text = message;
            }
        }

        void InstanceSelectClick(object sender, RoutedEventArgs e)
        {
            SelectInstance();
        }
    }
}
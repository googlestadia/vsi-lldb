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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
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
        readonly GameletSelectionWindow.Factory _gameletSelectionWindowFactory;
        readonly ICoreListRequest _coreListRequest;
        readonly ICloudRunner _cloudRunner;
        readonly GameletClient.Factory _gameletClientFactory;
        readonly IRemoteFile _remoteFile;
        readonly ISshManager _sshManager;
        readonly IDialogUtil _dialogUtil;
        readonly ActionRecorder _actionRecorder;
        readonly DebugSessionMetrics _debugSessionMetrics;
        readonly DebugEngine.DebugEngine.Params.Factory _paramsFactory;
        Gamelet _instance;

        public CoreAttachWindow(IServiceProvider serviceProvider)
        {
            var serviceManager = new ServiceManager();
            _taskContext = serviceManager.GetJoinableTaskContext();

            _dialogUtil = new DialogUtil();
            IExtensionOptions options =
                ((YetiVSIService) serviceManager.RequireGlobalService(typeof(YetiVSIService)))
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
            IRemoteCommand remoteCommand = new RemoteCommand(managedProcessFactory);
            var socketSender = new LocalSocketSender();
            _remoteFile = new RemoteFile(managedProcessFactory, transportSessionFactory: null,
                                         socketSender: socketSender, fileSystem: new FileSystem());
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
            _gameletSelectionWindowFactory = new GameletSelectionWindow.Factory();
            _paramsFactory = new DebugEngine.DebugEngine.Params.Factory(jsonUtil);
        }

        // Select a gamelet. If there are more than one gamelets reserved, a dialog would pop up
        // letting the user pick one.
        public void SelectInstance()
        {
            List<Gamelet> instances;
            try
            {
                var action = _actionRecorder.CreateToolAction(ActionType.GameletsList);
                var gameletClient = _gameletClientFactory.Create(_cloudRunner.Intercept(action));
                var queryTask = _cancelableTaskFactory.Create(
                    "Querying instances...", async () => await gameletClient.ListGameletsAsync());
                if (!queryTask.RunAndRecord(action))
                {
                    return;
                }

                instances = queryTask.Result;
            }
            catch (CloudException e)
            {
                Trace.Write("An exception was thrown while querying instances." +
                            Environment.NewLine + e);
                GameletMessageTextBox.Text = ErrorStrings.FailedToRetrieveGamelets(e.Message);
                return;
            }

            switch (instances.Count)
            {
                case 0:
                    _instance = null;
                    GameletMessageTextBox.Text = ErrorStrings.NoGameletsFound;
                    return;
                case 1:
                    _instance = instances[0];
                    break;
                default:
                    _instance = _gameletSelectionWindowFactory.Create(instances).Run();
                    if (_instance == null)
                    {
                        return;
                    }

                    break;
            }

            try
            {
                var action = _actionRecorder.CreateToolAction(ActionType.GameletEnableSsh);
                var enableSshTask = _cancelableTaskFactory.Create(
                    TaskMessages.EnablingSSH,
                    async _ => { await _sshManager.EnableSshAsync(_instance, action); });
                if (!enableSshTask.RunAndRecord(action))
                {
                    return;
                }
            }
            catch (Exception e) when (e is CloudException || e is SshKeyException)
            {
                Trace.Write("An exception was thrown while enabling ssh." + Environment.NewLine +
                            e);
                GameletMessageTextBox.Text = ErrorStrings.FailedToEnableSsh(e.Message);
                return;
            }

            GameletLabel.Content = "Instance: " + _instance.Id;
            RefreshCoreList();
        }

        void RefreshCoreList()
        {
            if (_instance == null)
            {
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
                Trace.WriteLine($"Unable to query instance crash dumps: {e}");
                GameletMessageTextBox.Text = ErrorStrings.ErrorQueryingCoreFiles(e.Message);
                CoreList.ItemsSource = new List<CoreListEntry>();
            }
            finally
            {
                Cursor = null;
            }
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
                    Trace.WriteLine($"Failed to download core file.{Environment.NewLine}" +
                                    $"{ex}");
                    _dialogUtil.ShowError(ErrorStrings.FailedToDownloadCore(ex.Message),
                                          ex.ToString());
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
                    var parameters = _paramsFactory.Create();
                    parameters.CoreFilePath = coreFilePath;
                    parameters.DebugSessionId = _debugSessionMetrics.DebugSessionId;
                    parameters.DeleteCoreFile = deleteAfter;
                    debugTargets[0].bstrOptions = _paramsFactory.Serialize(parameters);
                    debugTargets[0].guidLaunchDebugEngine = YetiConstants.DebugEngineGuid;
                    VsDebugTargetProcessInfo[] processInfo =
                        new VsDebugTargetProcessInfo[debugTargets.Length];
                    vsDebugger.LaunchDebugTargets4(1, debugTargets, processInfo);
                });
            }
            catch (COMException except)
            {
                Trace.WriteLine($"Failed to start debugger: {except}");

                // Both DebugEngine and Visual Studio already show error dialogs if DebugEngine
                // has to abort while it's attaching, no need to show another dialog in that case.
                if (except.ErrorCode != VSConstants.E_ABORT)
                {
                    _dialogUtil.ShowError(ErrorStrings.FailedToStartDebugger, except.ToString());
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
            if (e.AddedItems[0] == GameletTab)
            {
                RefreshCoreList();
            }

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
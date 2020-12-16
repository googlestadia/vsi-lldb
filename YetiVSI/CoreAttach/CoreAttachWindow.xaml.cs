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
        readonly IServiceProvider serviceProvider;

        readonly JoinableTaskContext taskContext;
        readonly CancelableTask.Factory cancelableTaskFactory;
        readonly ManagedProcess.Factory managedProcessFactory;
        readonly GameletSelectionWindow.Factory gameletSelectionWindowFactory;
        readonly ICoreListRequest coreListRequest;
        readonly SdkConfig.Factory sdkConfigFactory;
        readonly IExtensionOptions options;
        readonly ICloudRunner cloudRunner;
        readonly GameletClient.Factory gameletClientFactory;
        readonly IRemoteCommand remoteCommand;
        readonly IRemoteFile remoteFile;
        readonly ISshManager sshManager;
        readonly IDialogUtil dialogUtil;
        readonly ActionRecorder actionRecorder;
        readonly DebugSessionMetrics debugSessionMetrics;
        readonly ServiceManager serviceManager;
        readonly DebugEngine.DebugEngine.Params.Factory paramsFactory;
        Gamelet gamelet;

        public CoreAttachWindow(IServiceProvider serviceProvider)
        {
            serviceManager = new ServiceManager();
            taskContext = serviceManager.GetJoinableTaskContext();

            this.serviceProvider = serviceProvider;
            dialogUtil = new DialogUtil();
            options = ((YetiVSIService)serviceManager.RequireGlobalService(
                    typeof(YetiVSIService))).Options;
            managedProcessFactory = new ManagedProcess.Factory();
            var progressDialogFactory = new ProgressDialog.Factory();
            cancelableTaskFactory = new CancelableTask.Factory(taskContext, progressDialogFactory);
            coreListRequest = new CoreListRequest.Factory().Create();
            var jsonUtil = new JsonUtil();
            var credentialConfigFactory = new CredentialConfig.Factory(jsonUtil);
            var accountOptionLoader = new VsiAccountOptionLoader(options);
            var credentialManager =
                new CredentialManager(credentialConfigFactory, accountOptionLoader);
            remoteCommand = new RemoteCommand(managedProcessFactory);
            var socketSender = new LocalSocketSender();
            remoteFile = new RemoteFile(managedProcessFactory,
                transportSessionFactory:null,
                socketSender:socketSender,
                fileSystem:new FileSystem());
            var cloudConnection = new CloudConnection();
            sdkConfigFactory = new SdkConfig.Factory(jsonUtil);
            // NOTE: the lifetime of this CloudRunner is limited to the current CoreAttachWindow.
            cloudRunner = new CloudRunner(sdkConfigFactory, credentialManager, cloudConnection,
                                          new GgpSDKUtil());
            gameletClientFactory = new GameletClient.Factory();
            var sshKeyLoader = new SshKeyLoader(managedProcessFactory);
            var sshKnownHostsWriter = new SshKnownHostsWriter();
            sshManager = new SshManager(gameletClientFactory, cloudRunner, sshKeyLoader,
                sshKnownHostsWriter, remoteCommand);
            debugSessionMetrics = new DebugSessionMetrics(
                serviceProvider.GetService(typeof(SMetrics)) as IMetrics);
            debugSessionMetrics.UseNewDebugSessionId();
            actionRecorder = new ActionRecorder(debugSessionMetrics);

            InitializeComponent();
            gameletSelectionWindowFactory = new GameletSelectionWindow.Factory();
            paramsFactory = new DebugEngine.DebugEngine.Params.Factory(jsonUtil);
        }

        // Select a gamelet. If there are more than one gamelets reserved, a dialog would pop up
        // letting the user pick one.
        public void SelectGamelet()
        {
            List<Gamelet> gamelets;
            try
            {
                var action = actionRecorder.CreateToolAction(ActionType.GameletsList);
                var gameletClient = gameletClientFactory.Create(cloudRunner.Intercept(action));
                var queryTask = cancelableTaskFactory
                    .Create("Querying instances...",
                        async () => await gameletClient.ListGameletsAsync());
                if (!queryTask.RunAndRecord(action))
                {
                    return;
                }
                gamelets = queryTask.Result;
            }
            catch (CloudException e)
            {
                Trace.Write("An exception was thrown while querying instances." +
                    Environment.NewLine + e.ToString());
                gameletMessageTextBox.Text = ErrorStrings.FailedToRetrieveGamelets(e.Message);
                return;
            }
            switch (gamelets.Count)
            {
                case 0:
                    gamelet = null;
                    gameletMessageTextBox.Text = ErrorStrings.NoGameletsFound;
                    return;
                case 1:
                    gamelet = gamelets[0];
                    break;
                default:
                    gamelet = gameletSelectionWindowFactory.Create(gamelets).Run();
                    if (gamelet == null)
                    {
                        return;
                    }
                    break;
            }
            try
            {
                var action = actionRecorder.CreateToolAction(ActionType.GameletEnableSsh);
                var enableSshTask = cancelableTaskFactory.Create(TaskMessages.EnablingSSH,
                    async _ =>
                    {
                        await sshManager.EnableSshAsync(gamelet, action);
                    });
                if (!enableSshTask.RunAndRecord(action))
                {
                    return;
                }
            }
            catch (Exception e) when (e is CloudException || e is SshKeyException)
            {
                Trace.Write("An exception was thrown while enabling ssh." +
                    Environment.NewLine + e.ToString());
                gameletMessageTextBox.Text = ErrorStrings.FailedToEnableSsh(e.Message);
                return;
            }
            gameletLabel.Content = "Instance: " + gamelet.Id;
            RefreshCoreList();
        }

        private void RefreshCoreList()
        {
            if (gamelet == null)
            {
                return;
            }
            Cursor = System.Windows.Input.Cursors.Wait;
            try
            {
                var queryTaskTitle = "Querying instance crash dumps...";
                var queryTask = cancelableTaskFactory.Create(queryTaskTitle,
                        async () => await coreListRequest.GetCoreListAsync(new SshTarget(gamelet)));

                // Ignore cancelation, and accept the empty result.
                queryTask.RunAndRecord(actionRecorder, ActionType.CrashDumpList);
                coreList.ItemsSource = queryTask.Result;
            }
            catch (ProcessException e)
            {
                Trace.WriteLine("Unable to query instance crash dumps: " + e.ToString());
                gameletMessageTextBox.Text = ErrorStrings.ErrorQueryingCoreFiles(e.Message);
                coreList.ItemsSource = new List<CoreListEntry>();
            }
            finally
            {
                Cursor = null;
            }
        }

        private void coreListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            attachButton.IsEnabled = coreList.SelectedItem != null;
        }

        private void refreshClick(object sender, RoutedEventArgs e)
        {
            RefreshCoreList();
        }

        private void cancelClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void attachClick(object sender, RoutedEventArgs e)
        {
            taskContext.ThrowIfNotOnMainThread();

            // Figure out core file path
            CoreListEntry? coreListEntry = null;
            if (tabControl.SelectedContent == gameletGroupBox)
            {
                coreListEntry = (CoreListEntry?)coreList.SelectedItem;
                if (coreListEntry == null)
                {
                    dialogUtil.ShowError(ErrorStrings.NoCoreFileSelected);
                    return;
                }
            }
            string coreFilePath = null;
            bool deleteAfter = false;
            if (tabControl.SelectedContent == localGroupBox)
            {
                coreFilePath = localCorePathBox.Text;
            }
            else if (tabControl.SelectedContent == gameletGroupBox)
            {
                var tempPath = Path.GetTempPath();
                try
                {
                    cancelableTaskFactory
                        .Create(TaskMessages.DownloadingCoreFile,
                            async task => await remoteFile.GetAsync(new SshTarget(gamelet),
                                "/usr/local/cloudcast/core/" + coreListEntry?.Name, tempPath,
                                task))
                        .RunAndRecord(actionRecorder, ActionType.CrashDumpDownload);
                }
                catch (ProcessException ex)
                {
                    Trace.WriteLine($"Failed to download core file.{Environment.NewLine}" +
                        $"{ex.ToString()}");
                    dialogUtil.ShowError(ErrorStrings.FailedToDownloadCore(ex.Message),
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
                (IVsDebugger4)ServiceProvider.GlobalProvider.GetService(typeof(IVsDebugger));
            if (vsDebugger == null)
            {
                ShowMessage(ErrorStrings.FailedToStartDebugger);
                return;
            }

            try
            {
                actionRecorder.RecordToolAction(ActionType.CrashDumpAttach,
                    delegate
                    {
                        taskContext.ThrowIfNotOnMainThread();

                        VsDebugTargetInfo4[] debugTargets = new VsDebugTargetInfo4[1];
                        debugTargets[0].dlo = (uint)DEBUG_LAUNCH_OPERATION.DLO_CreateProcess;

                        // LaunchDebugTargets4() throws an exception if |bstrExe| and |bstrCurDir|
                        // are empty. Use core path and temp directory as placeholders.
                        debugTargets[0].bstrExe = coreFilePath;
                        debugTargets[0].bstrCurDir = Path.GetTempPath();
                        var parameters = paramsFactory.Create();
                        parameters.CoreFilePath = coreFilePath;
                        parameters.DebugSessionId = debugSessionMetrics.DebugSessionId;
                        parameters.DeleteCoreFile = deleteAfter;
                        debugTargets[0].bstrOptions = paramsFactory.Serialize(parameters);
                        debugTargets[0].guidLaunchDebugEngine = YetiConstants.DebugEngineGuid;
                        VsDebugTargetProcessInfo[] processInfo =
                            new VsDebugTargetProcessInfo[debugTargets.Length];
                        vsDebugger.LaunchDebugTargets4(1, debugTargets, processInfo);
                    });
            }
            catch (COMException except)
            {
                Trace.WriteLine($"Failed to start debugger: {except.ToString()}");

                // Both DebugEngine and Visual Studio already show error dialogs if DebugEngine
                // has to abort while it's attaching, no need to show another dialog in that case.
                if (except.ErrorCode != VSConstants.E_ABORT)
                {
                    dialogUtil.ShowError(ErrorStrings.FailedToStartDebugger, except.ToString());
                }
            }
            finally
            {
                Close();
            }
        }

        private void browseClick(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Core files (*.core, *.dmp)|*.core; *.dmp|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                localCorePathBox.Text = openFileDialog.FileName;
            }
        }

        private void tabSelected(object sender, SelectionChangedEventArgs e)
        {
            // |e| has the newly selected tab while |tabControl.SelectedContent| has the
            // previously selected content.
            if (e.AddedItems.Count == 0)
            {
                return;
            }
            gameletMessageTextBox.Text = "";
            localMessageTextBox.Text = "";
            if (e.AddedItems[0] == gameletTab)
            {
                RefreshCoreList();
            }
            if (e.AddedItems[0] == localTab)
            {
                attachButton.IsEnabled = File.Exists(localCorePathBox.Text);
            }
        }

        private void localCorePathBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            attachButton.IsEnabled = File.Exists(localCorePathBox.Text);
            if (!string.IsNullOrEmpty(localCorePathBox.Text) && !File.Exists(localCorePathBox.Text))
            {
                localMessageTextBox.Text = ErrorStrings.CoreFileDoesNotExist;
            }
        }

        private void ShowMessage(string message)
        {
            if (tabControl.SelectedContent == localGroupBox)
            {
                localMessageTextBox.Text = message;
            }
            else
            {
                gameletMessageTextBox.Text = message;
            }
        }

        private void gameletSelectClick(object sender, RoutedEventArgs e)
        {
            SelectGamelet();
        }
    }
}

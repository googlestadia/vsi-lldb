using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using Metrics.Shared;
using YetiCommon;
using YetiCommon.Logging;
using YetiVSI.Metrics;
using Task = System.Threading.Tasks.Task;

namespace YetiVSI
{
    class ReportBug
    {
        readonly static string _description_template = @"
Information:
* Stadia for Visual Studio version: __VSIX_VERSION__
* Visual Studio version: __VS_VERSION__

For Run/Debug related issues, attach the log files from __LOG_PATH__
__CURRENT_LOG__
---

Explain your issue here.
__DETAILS__
".Trim();

        static ReportBug _reportBug;

        readonly ActionRecorder _actionRecorder;
        readonly string _vsVersion;
        readonly string _projectId;

        ReportBug(IVsiMetrics metrics, string vsVersion, string projectId)
        {
            var dsm = new DebugSessionMetrics(metrics);
            dsm.UseNewDebugSessionId();
            _actionRecorder = new ActionRecorder(dsm);
            _vsVersion = vsVersion;
            _projectId = projectId;
        }

        public void Execute(string details)
        {
            _actionRecorder.RecordSuccess(ActionType.ReportFeedback);

            var currentLog = YetiLog.IsInitialized
                ? $"Current log file is {YetiLog.CurrentLogFile.NormalizePath()}\n"
                : "";

            var detailsMsg = string.IsNullOrEmpty(details)
                ? ""
                : $"\n---\n{details}";

            var description = _description_template
                .Replace("__VSIX_VERSION__", Versions.GetExtensionVersion())
                .Replace("__VS_VERSION__", _vsVersion)
                .Replace("__LOG_PATH__", SDKUtil.GetLoggingPath())
                .Replace("__CURRENT_LOG__", currentLog)
                .Replace("__DETAILS__", detailsMsg);

            var queryParams = new Dictionary<string, string>
            {
                { "subject", "Problem with Stadia for Visual Studio" },
                { "description", description },
                { "category", "Developer / Development / API Question" },
                { "subcategory", "Visual Studio" },
                { "sdk_version", Versions.GetSdkVersion().ToString() },
                { "project_id", _projectId }
            };

            string QueryEncode(string input)
            {
                return Uri.EscapeDataString(input.Replace(Environment.NewLine, "\n"));
            }
            var query = string.Join(
                "&", queryParams.Select(p => $"{QueryEncode(p.Key)}={QueryEncode(p.Value)}"));
            var url = @"https://community.stadia.dev/s/contact-support-form?" + query;

            // Open the URL in the default browser.
            // Disposing the process object doesn't kill the actual process.
            Process.Start(url).Dispose();
        }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            var metrics = (IVsiMetrics)await package.GetServiceAsync(typeof(SMetrics));
            var vsVersion = await VsVersion.GetVisualStudioVersionAsync(package);

            var configFactory = new SdkConfig.Factory(new JsonUtil());
            var projectId = configFactory.LoadOrDefault().ProjectId;

            _reportBug = new ReportBug(metrics, vsVersion, projectId);

#pragma warning disable VSSDK006 // IMenuCommandService must always exist.
            var mcs =
                (IMenuCommandService)await package.GetServiceAsync(typeof(IMenuCommandService));
#pragma warning restore VSSDK006

            var cmdId = new CommandID(YetiConstants.CommandSetGuid, PkgCmdID.cmdidReportBug);
            mcs.AddCommand(new MenuCommand((s, e) => _reportBug.Execute(null), cmdId));
        }

        public static void TriggerCommand(string details)
        {
            _reportBug?.Execute(details);
        }
    }
}

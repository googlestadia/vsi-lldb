using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Design;
using System.Diagnostics;
using YetiCommon;
using YetiVSI.Metrics;
using YetiVSI.Shared.Metrics;
using Task = System.Threading.Tasks.Task;

namespace YetiVSI
{
    class ReportBug
    {
        private static ReportBug _reportBug;

        private ActionRecorder _actionRecorder;

        private ReportBug(IMetrics metrics)
        {
            var dsm = new DebugSessionMetrics(metrics);
            dsm.UseNewDebugSessionId();
            _actionRecorder = new ActionRecorder(dsm);
        }

        public void Execute()
        {
            _actionRecorder.RecordSuccess(ActionType.ReportFeedback);

            // Open the URL in the default browser.
            // Disposing the process object doesn't kill the actual process.
            var p = Process.Start(@"https://community.stadia.dev/s/contactsupport");
            p.Dispose();
        }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            var metrics = (IMetrics)await package.GetServiceAsync(typeof(SMetrics));
            _reportBug = new ReportBug(metrics);

#pragma warning disable VSSDK006 // IMenuCommandService must always exist.
            var mcs =
                (IMenuCommandService)await package.GetServiceAsync(typeof(IMenuCommandService));
#pragma warning restore VSSDK006

            var cmdId = new CommandID(YetiConstants.CommandSetGuid, PkgCmdID.cmdidReportBug);
            mcs.AddCommand(new MenuCommand((s, e) => _reportBug.Execute(), cmdId));
        }

        public static void TriggerCommand()
        {
            _reportBug?.Execute();
        }
    }
}

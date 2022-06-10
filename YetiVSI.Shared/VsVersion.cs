using Microsoft.VisualStudio;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace YetiVSI
{
    class VsVersion
    {
        [Guid("1EAA526A-0898-11d3-B868-00C04F79F802")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IVsAppId
        {
            [PreserveSig]
            int SetSite(Microsoft.VisualStudio.OLE.Interop.IServiceProvider pSP);
            [PreserveSig]
            int GetProperty(int propid, [MarshalAs(UnmanagedType.Struct)] out object pvar);
            [PreserveSig]
            int SetProperty(int propid, [MarshalAs(UnmanagedType.Struct)] object var);
            [PreserveSig]
            int GetGuidProperty(int propid, out Guid guid);
            [PreserveSig]
            int SetGuidProperty(int propid, ref Guid rguid);
            [PreserveSig]
            int Initialize();
        }
        const int VSAPROPID_ProductSemanticVersion = -8642;

        public static string GetVisualStudioVersion()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var vsAppId = Package.GetGlobalService(typeof(IVsAppId)) as IVsAppId;
            return GetVersion(vsAppId);
        }

        public static async Task<string> GetVisualStudioVersionAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var vsAppId = await package.GetServiceAsync(typeof(IVsAppId)) as IVsAppId;
            return GetVersion(vsAppId);
        }

        private static string GetVersion(IVsAppId vsAppId)
        {
            if (vsAppId == null)
            {
                return "unknown";
            }

            // Returns something like: "15.9.35+28307.1500" which also contains Micro version.
            // See: https://stackoverflow.com/a/55039958
            int result = vsAppId.GetProperty(VSAPROPID_ProductSemanticVersion, out var versionObj);
            if (result != VSConstants.S_OK)
            {
                return "unknown";
            }

            var version = versionObj.ToString();
            var parts = version.Split('+', '-');
            if (parts.Length == 2)
            {
                return parts[0];
            }
            return version;
        }
    }
}

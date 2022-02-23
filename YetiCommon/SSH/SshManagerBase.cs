using GgpGrpc.Cloud;
using GgpGrpc.Models;
using System.Diagnostics;
using System.Threading.Tasks;

namespace YetiCommon.SSH
{
    // Used to enable SSH on a gamelet, by uploading a locally generated development SSH key.
    public class SshManagerBase
    {
        protected readonly ISshKeyLoader sshKeyLoader;
        protected readonly ISshKnownHostsWriter sshKnownHostsWriter;
        protected readonly IRemoteCommand remoteCommand;

        public SshManagerBase(ISshKeyLoader sshKeyLoader,
            ISshKnownHostsWriter sshKnownHostsWriter, IRemoteCommand remoteCommand)
        {
            this.sshKeyLoader = sshKeyLoader;
            this.sshKnownHostsWriter = sshKnownHostsWriter;
            this.remoteCommand = remoteCommand;
        }

        public async Task EnableSshForGameletAsync(Gamelet gamelet, IGameletClient gameletClient)
        {
            var sshKey = await sshKeyLoader.LoadOrCreateAsync();
            sshKnownHostsWriter.CreateOrUpdate(gamelet);

            // Try to optimistically connect, but only if we already have a key file.
            try
            {
                await remoteCommand.RunWithSuccessAsync(new SshTarget(gamelet), "/bin/true");
                return;
            }
            catch (ProcessException e)
            {
                Trace.WriteLine(
                    $"SSH check failed; fallback to calling EnableSSH; error: {e}");
            }
            await gameletClient.EnableSshAsync(gamelet.Id, sshKey.PublicKey);
        }
    }
}

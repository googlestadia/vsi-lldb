using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace YetiCommon.Logging
{
    public static class TextWriterExtensions
    {
        public static async Task WriteLogAsync(this TextWriter log, string message)
        {
            Task loggingTask = log.WriteLineAsync(message);
            Trace.WriteLine(message);
            await loggingTask;
        }
    }
}

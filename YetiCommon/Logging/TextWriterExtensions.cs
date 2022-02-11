using System.Diagnostics;
using System.IO;

namespace YetiCommon.Logging
{
    public static class TextWriterExtensions
    {
        public static void WriteLineAndTrace(this TextWriter log, string message)
        {
            log.WriteLine(message);
            Trace.WriteLine(message);
        }
    }
}

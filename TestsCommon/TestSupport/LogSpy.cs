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

using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace TestsCommon.TestSupport
{
    // Captures log output in unit tests.
    //
    // Example usage:
    //   var logSpy = new LogSpy();
    //   logSpy.Attach();
    //
    //   // Exercise system under test
    //
    //   Assert.That(logSpy.GetOutput().Contains("Some Expected String"));
    //
    //   logSpy.Detach();
    public class LogSpy : TraceListener
    {
        private StringBuilder outputBuilder;

        private TextWriterTraceListener textTraceListener;

        public LogSpy()
        {
            outputBuilder = new StringBuilder();
            textTraceListener = new TextWriterTraceListener(new StringWriter(outputBuilder));
        }

        public void Attach()
        {
            Trace.Listeners.Add(this);
        }

        public void Detach()
        {
            Trace.Listeners.Remove(this);
        }

        // Returns all the log output captured.
        public string GetOutput()
        {
            textTraceListener.Flush();
            return outputBuilder.ToString();
        }

        /// <summary>
        /// Return a section of the log output.
        /// </summary>
        /// <param name="offset">The offset at which to start reading. A negative value will
        /// be interpreted as an offset relative to the end. A negative offset which is larger
        /// than the length of the string will be interpreted as 0</param>
        /// <param name="limit">The number of characters to return. A negative value will return as
        /// many characters as possible.</param>
        public string GetOutput(int offset, int limit)
        {
            textTraceListener.Flush();
            if (offset < 0)
            {
                offset = Math.Max(outputBuilder.Length + offset, 0);
            }

            if (limit < 0)
            {
                limit = outputBuilder.Length - offset;
            }
            return outputBuilder.ToString(offset, limit);
        }

        public void Clear()
        {
            textTraceListener.Flush();
            outputBuilder.Clear();
        }

        #region TraceListener

        public override void Write(string message)
        {
            textTraceListener.Write(message);
        }

        public override void WriteLine(string message)
        {
            textTraceListener.WriteLine(message);
        }

        #endregion
    }
}

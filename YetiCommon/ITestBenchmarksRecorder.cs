// Copyright 2021 Google LLC
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

namespace YetiCommon
{
    /// <summary>
    /// Used to record simple benchmarks during performance tests from inside the DebugEngine and
    /// in performance tests.
    /// </summary>
    public interface ITestBenchmarksRecorder
    {
        void StartMetricRecording(string metricName);
        void EndMetricRecording(string metricName);

        void AddAdditionalInfo(string metricName, string additionalInfoKey,
                               string additionalInfoValue);

        /// <summary>
        /// Prefix that gets prepended to all |metricName|s.
        /// </summary>
        string Prefix { get; set; }
    }

    /// <summary>
    /// Scoped metric for test benchmarks recording. All metrics inside this scope will have the
    /// parent metric name prefixed, e.g. "ParentMetric - ChildMetric - GrandChildMetric".
    /// Automatically calls StartMetricRecording() and EndMetricRecording() if used with a "using"
    /// statement.
    /// </summary>
    public class TestBenchmark : IDisposable
    {
        readonly ITestBenchmarksRecorder _recorder;
        readonly string _metricName;
        readonly string _parentPrefix;

        /// <summary>
        /// Creates a new scoped test benchmark metric.
        /// </summary>
        /// <param name="recorder">Recorder to use. If null, no benchmark is recorded.</param>
        /// <param name="metricName">
        /// The metric name to use. Gets prepended by the parent scope's name, e.g.
        /// "ParentMetric - ChildMetric".
        /// </param>
        public TestBenchmark(ITestBenchmarksRecorder recorder, string metricName)
        {
            _recorder = recorder;
            _metricName = metricName;

            if (_recorder == null)
            {
                return;
            }

            _parentPrefix = _recorder.Prefix;
            string newPrefix = (_parentPrefix ?? "") + _metricName + " - ";
            _recorder.StartMetricRecording(_metricName);
            _recorder.Prefix = newPrefix;
        }

        public void AddAdditionalInfo(string additionalInfoKey, string additionalInfoValue)
        {
            if (_recorder == null)
            {
                return;
            }

            // Note: Need to restore the previous prefix in order to add additional info.
            string curPrefix = _recorder.Prefix;
            _recorder.Prefix = _parentPrefix;
            _recorder.AddAdditionalInfo(_metricName, additionalInfoKey, additionalInfoValue);
            _recorder.Prefix = curPrefix;
        }

        public void Dispose()
        {
            if (_recorder == null)
            {
                return;
            }

            _recorder.Prefix = _parentPrefix;
            _recorder.EndMetricRecording(_metricName);
        }
    }
}
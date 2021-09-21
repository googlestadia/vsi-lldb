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
    /// Records simple benchmarks during performance tests.
    /// </summary>
    public interface ITestBenchmarksRecorder
    {
        /// <summary>
        /// Starts a new performance benchmark.
        /// </summary>
        /// <param name="metricName">Name of the benchmark.</param>
        void StartMetricRecording(string metricName);

        /// <summary>
        /// Ends an existing performance benchmark.
        /// </summary>
        /// <param name="metricName">Name of the benchmark.</param>
        void EndMetricRecording(string metricName);

        /// <summary>
        /// Adds generic additional information to a test benchmark metric.
        /// </summary>
        /// <param name="metricName">
        /// Name of the metric to attach information to.
        /// </param>
        /// <param name="additionalInfoKey">
        /// Name of the information to add.
        /// </param>
        /// <param name="additionalInfoValue">
        /// Value of the information to add.
        /// </param>
        void AddAdditionalInfo(string metricName, string additionalInfoKey,
                               string additionalInfoValue);

        /// <summary>
        /// Prefix that gets prepended to all |metricName|s.
        /// </summary>
        string Prefix { get; set; }
    }

    /// <summary>
    /// Sets a global ITestBenchmarksRecorder on construction that can be by the debug engine to
    /// produce fine-grained performance benchmarks. Restores the previous recorder on disposal.
    /// Usage:
    /// 
    ///   // This code lives in a performance test.
    ///   using (new TestBenchmarkScope(recorder)) {
    ///     // Call into debug engine.
    ///   }
    ///
    ///   // This code lives anywhere in the debug engine.
    ///   void SomeWorkInDebugEngine() {
    ///     using (new TestBenchmark("MyMetric", TestBenchmarkScope.Recorder)) {
    ///     {
    ///       // Do work
    ///     }
    ///   }
    /// 
    /// TestBenchmarkScopes should only be created in performance tests. By default,
    /// TestBenchmarkScope.Recorder is null and TestBenchmark is a no-op for a null recorder.
    /// This is the case in production code.
    ///
    /// All TestBenchmarks created in a TestBenchmarkScope must be disposed in the same scope.
    /// This is usually not an issue and enforced by the use of 'using' statements.
    /// </summary>
    public class TestBenchmarkScope : IDisposable
    {
        public static ITestBenchmarksRecorder Recorder { get; private set; }

        readonly ITestBenchmarksRecorder _prevRecorder;

        public TestBenchmarkScope(ITestBenchmarksRecorder recorder)
        {
            _prevRecorder = recorder;
            Recorder = recorder;
        }

        public void Dispose()
        {
            Recorder = _prevRecorder;
        }
    }

    /// <summary>
    /// Scoped metric for test benchmarks recording. All metrics inside this scope have the parent
    /// metric name prefixed, e.g. "ParentMetric - ChildMetric - GrandChildMetric".
    /// 
    /// Automatically calls StartMetricRecording() and EndMetricRecording() if used with a "using"
    /// statement. See TestBenchmarkScope for a usage example.
    /// </summary>
    public class TestBenchmark : IDisposable
    {
        readonly string _metricName;
        readonly string _parentPrefix;
        readonly ITestBenchmarksRecorder _recorder;

        /// <summary>
        /// Creates a new scoped TestBenchmark.
        /// </summary>
        /// <param name="metricName">Name of the metric to create.</param>
        /// <param name="recorder">Benchmarks recorder to use.</param>
        public TestBenchmark(string metricName, ITestBenchmarksRecorder recorder)
        {
            _metricName = metricName;
            _recorder = recorder;

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
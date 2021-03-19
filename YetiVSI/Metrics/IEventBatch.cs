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

namespace YetiVSI.Metrics
{
    public interface IEventBatch<TParams, TSummary>
    {
        /// <summary>
        /// Adds a new event. Not thread-safe.
        /// </summary>
        /// <param name="batchParams">Specific parameters according to the batch events to be
        /// collected.</param>
        /// <remarks>
        /// Implementations should prioritize performance. CPU-intensive work should be postponed
        /// to when GetLogEventAndType gets called.
        /// </remarks>
        void Add(TParams batchParams);

        /// <summary>
        /// Gets a summary according to the events collected in the batch.
        /// </summary>
        TSummary GetSummary();
    }
}

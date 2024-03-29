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

namespace Metrics.Shared
{
    public interface IBaseMetrics
    {
        // Non-blocking call to record the given event type and proto.
        void RecordEvent(DeveloperEventType.Types.Type type, DeveloperLogEvent proto);
    }

    // Provides methods to record metric actions.
    //
    // Don't use this interface direclty. Instantiate an ActionRecorder object.
    public interface IVsiMetrics : IBaseMetrics
    {
        // Create and store a new debug session ID that can be recorded in log events.
        string NewDebugSessionId();
    }
}

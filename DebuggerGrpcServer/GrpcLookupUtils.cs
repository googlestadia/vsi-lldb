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

using Debugger.Common;
using Grpc.Core;
using LldbApi;
using System.Collections.Concurrent;

namespace DebuggerGrpcServer
{
    // Utility functions that are used to lookup LLDB objects from GRPC objects.
    class GrpcLookupUtils
    {
        internal static SbProcess GetProcess(GrpcSbProcess grpcProcess,
            ConcurrentDictionary<int, SbProcess> processStore)
        {
            SbProcess sbProcess = null;
            if (!processStore.TryGetValue(grpcProcess.Id, out sbProcess))
            {
                ErrorUtils.ThrowError(StatusCode.Internal, "Could not find process in store: "
                    + grpcProcess.Id);
            }
            return sbProcess;
        }

        internal static RemoteTarget GetTarget(GrpcSbTarget grpcSbTarget,
            ConcurrentDictionary<long, RemoteTarget> targetStore)
        {
            RemoteTarget remoteTarget = null;
            if (!targetStore.TryGetValue(grpcSbTarget.Id, out remoteTarget))
            {
                ErrorUtils.ThrowError(StatusCode.Internal, "Could not find target by ID: "
                    + grpcSbTarget.Id);
            }
            return remoteTarget;
        }
    }
}

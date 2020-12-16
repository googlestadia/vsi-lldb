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
using Debugger.Common;
using DebuggerApi;

namespace DebuggerGrpcClient
{
    // Creates SbEvent objects.
    class GrpcEventFactory
    {
        public SbEvent Create(GrpcSbEvent grpcSbEvent)
        {
            if (grpcSbEvent == null) { throw new ArgumentNullException(nameof(grpcSbEvent));}
            return new SbEventImpl(grpcSbEvent);
        }
    }

    // Implementation of the SBEvent interface that uses GRPC to make RPCs to a remote endpoint.
    class SbEventImpl : SbEvent
    {
        readonly GrpcSbEvent grpcSbEvent;

        internal SbEventImpl(GrpcSbEvent grpcSbEvent)
        {
            this.grpcSbEvent = grpcSbEvent;
        }

        #region SbEvent

        public EventType GetEventType()
        {
            return (EventType)grpcSbEvent.Type;
        }

        public string GetDescription()
        {
            return grpcSbEvent.Description;
        }

        public StateType GetStateType()
        {
            switch (grpcSbEvent.StateType)
            {
                case GrpcSbEvent.Types.StateType.Connected:
                    return StateType.CONNECTED;
                case GrpcSbEvent.Types.StateType.Stopped:
                    return StateType.STOPPED;
                case GrpcSbEvent.Types.StateType.Running:
                    return StateType.RUNNING;
                case GrpcSbEvent.Types.StateType.Detached:
                    return StateType.DETACHED;
                case GrpcSbEvent.Types.StateType.Exited:
                    return StateType.EXITED;
                default:
                    return StateType.INVALID;
            }
        }

        public bool GetProcessRestarted()
        {
            return grpcSbEvent.HasProcessResumed;
        }

        #endregion
    }
}

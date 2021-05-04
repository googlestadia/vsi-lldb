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
using Debugger.SbListenerRpc;
using Grpc.Core;
using LldbApi;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using YetiVSI.DebugEngine;

namespace DebuggerGrpcServer
{
    // Server implementation of the SBTarget RPC.
    public class SbListenerRpcServiceImpl : SbListenerRpcService.SbListenerRpcServiceBase
    {
        readonly ConcurrentDictionary<long, SbListener> _listenerStore;
        readonly LLDBListenerFactory _listenerFactory;
        readonly LLDBBreakpointApi _breakpointApi;

        public SbListenerRpcServiceImpl(ConcurrentDictionary<long, SbListener> listenerStore) :
            this(listenerStore, new LLDBListenerFactory(), new LLDBBreakpointApi())
        {
        }

        // Constructor that can be used by tests to pass in mock objects.
        public SbListenerRpcServiceImpl(ConcurrentDictionary<long, SbListener> listenerStore,
                                        LLDBListenerFactory listenerFactory,
                                        LLDBBreakpointApi breakpointApi)
        {
            _listenerStore = listenerStore;
            _listenerFactory = listenerFactory;
            _breakpointApi = breakpointApi;
        }

        #region SbListenerRpcService.SbTargetRpcServiceBase

        public override Task<CreateResponse> Create(CreateRequest request,
            ServerCallContext context)
        {
            var listener = _listenerFactory.Create(request.Name);
            if (!_listenerStore.TryAdd(listener.GetId(), listener))
            {
                ErrorUtils.ThrowError(
                    StatusCode.Internal, "Could not add listener to store: " + listener.GetId());
            }
            var response =
                new CreateResponse{ Listener = new GrpcSbListener { Id = listener.GetId() } };
            return Task.FromResult(response);
        }

        public override Task<WaitForEventResponse> WaitForEvent(WaitForEventRequest request,
            ServerCallContext context)
        {
            SbListener listener;
            if (!_listenerStore.TryGetValue(request.Listener.Id, out listener))
            {
                ErrorUtils.ThrowError(
                    StatusCode.Internal,
                    "Could not find listener in store: " + request.Listener.Id);
            }

            SbEvent evnt;
            bool result = listener.WaitForEvent(request.NumSeconds, out evnt);
            var response = new WaitForEventResponse
            {
                Result = result,
            };
            if (result)
            {
                response.Event = new GrpcSbEvent
                {
                    Type = (uint)evnt.GetEventType(),
                    Description = evnt.GetDescription(),
                    HasProcessResumed = evnt.GetProcessRestarted(),
                    IsBreakpointEvent = _breakpointApi.EventIsBreakpointEvent(evnt)
                };
                if ((evnt.GetEventType() & EventType.STATE_CHANGED) != 0)
                {
                    response.Event.StateType = StateTypeToProto(evnt.GetStateType());
                }

                if (response.Event.IsBreakpointEvent)
                {
                    response.Event.BreakpointData = new GrpcEventBreakpointData
                    {
                        EventType = (uint)_breakpointApi.GetBreakpointEventTypeFromEvent(evnt),
                        BreakpointId = _breakpointApi.GetBreakpointFromEvent(evnt).GetId(),
                    };
                }
            }
            return Task.FromResult(response);
        }

        private static GrpcSbEvent.Types.StateType StateTypeToProto(StateType type)
        {
            switch (type)
            {
                case StateType.CONNECTED:
                    return GrpcSbEvent.Types.StateType.Connected;
                case StateType.STOPPED:
                    return GrpcSbEvent.Types.StateType.Stopped;
                case StateType.RUNNING:
                    return GrpcSbEvent.Types.StateType.Running;
                case StateType.DETACHED:
                    return GrpcSbEvent.Types.StateType.Detached;
                case StateType.EXITED:
                    return GrpcSbEvent.Types.StateType.Exited;
                default:
                    return GrpcSbEvent.Types.StateType.Invalid;
            }
        }

        #endregion

    }
}

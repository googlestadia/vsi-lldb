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

#include "LLDBEvent.h"

#include "lldb/API/SBProcess.h"
#include "lldb/API/SBStream.h"

namespace YetiVSI {
namespace DebugEngine {

LLDBEvent::LLDBEvent(lldb::SBEvent event) {
  event_ = MakeUniquePtr<lldb::SBEvent>(event);
}

EventType LLDBEvent::GetEventType() {
  uint32_t type = event_->GetType();
  EventType result;
  if (type & lldb::SBProcess::eBroadcastBitStateChanged) {
    result = result | EventType::STATE_CHANGED;
  }
  if (type & lldb::SBProcess::eBroadcastBitInterrupt) {
    result = result | EventType::INTERRUPT;
  }
  if (type & lldb::SBProcess::eBroadcastBitStructuredData) {
    result = result | EventType::STRUCTURED_DATA;
  }

  return result;
}

System::String ^ LLDBEvent::GetDescription() {
  lldb::SBStream description;
  if (event_->GetDescription(description)) {
    return gcnew System::String(description.GetData());
  }
  return gcnew System::String("");
}

StateType LLDBEvent::GetStateType() {
  switch (lldb::SBProcess::GetStateFromEvent(*(*event_))) {
    case lldb::eStateConnected:
      return StateType::CONNECTED;
    case lldb::eStateStopped:
      return StateType::STOPPED;
    case lldb::eStateRunning:
      return StateType::RUNNING;
    case lldb::eStateDetached:
      return StateType::DETACHED;
    case lldb::eStateExited:
      return StateType::EXITED;
    default:
      return StateType::INVALID;
  }
}

bool LLDBEvent::GetProcessRestarted() {
  return lldb::SBProcess::GetRestartedFromEvent(*(*event_));
}

}  // namespace DebugEngine
}  // namespace YetiVSI

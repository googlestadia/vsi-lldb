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

#include "LLDBBreakpointApi.h"

#include "LLDBBreakpoint.h"
#include "LLDBBreakpointLocation.h"
#include "LLDBEvent.h"
#include "lldb/API/SBStringList.h"

namespace YetiVSI {
namespace DebugEngine {

BreakpointEventType Convert(lldb::BreakpointEventType eventType) {
  BreakpointEventType result = (BreakpointEventType)0;
  if (eventType & lldb::eBreakpointEventTypeInvalidType) {
    result = result | BreakpointEventType::InvalidType;
  }
  if (eventType & lldb::eBreakpointEventTypeAdded) {
    result = result | BreakpointEventType::Added;
  }
  if (eventType & lldb::eBreakpointEventTypeRemoved) {
    result = result | BreakpointEventType::Removed;
  }
  if (eventType & lldb::eBreakpointEventTypeLocationsAdded) {
    result = result | BreakpointEventType::LocationsAdded;
  }
  if (eventType & lldb::eBreakpointEventTypeLocationsRemoved) {
    result = result | BreakpointEventType::LocationsRemoved;
  }
  if (eventType & lldb::eBreakpointEventTypeLocationsResolved) {
    result = result | BreakpointEventType::LocationsResolved;
  }
  if (eventType & lldb::eBreakpointEventTypeEnabled) {
    result = result | BreakpointEventType::Enabled;
  }
  if (eventType & lldb::eBreakpointEventTypeDisabled) {
    result = result | BreakpointEventType::Disabled;
  }
  if (eventType & lldb::eBreakpointEventTypeCommandChanged) {
    result = result | BreakpointEventType::CommandChanged;
  }
  if (eventType & lldb::eBreakpointEventTypeConditionChanged) {
    result = result | BreakpointEventType::ConditionChanged;
  }
  if (eventType & lldb::eBreakpointEventTypeIgnoreChanged) {
    result = result | BreakpointEventType::IgnoreChanged;
  }
  if (eventType & lldb::eBreakpointEventTypeThreadChanged) {
    result = result | BreakpointEventType::ThreadChanged;
  }
  if (eventType & lldb::eBreakpointEventTypeAutoContinueChanged) {
    result = result | BreakpointEventType::AutoContinueChanged;
  }
  return result;
}

BreakpointEventType LLDBBreakpointApi::GetBreakpointEventTypeFromEvent(SbEvent ^
                                                                    sbEvent) {
  LLDBEvent ^ lldb_event = safe_cast<LLDBEvent ^>(sbEvent);
  const lldb::SBEvent native_event = lldb_event->GetNativeObject();
  return Convert(lldb::SBBreakpoint::GetBreakpointEventTypeFromEvent(native_event));
}

SbBreakpoint ^ LLDBBreakpointApi::GetBreakpointFromEvent(SbEvent ^ sbEvent) {
  LLDBEvent ^ lldb_event = safe_cast<LLDBEvent ^>(sbEvent);
  const lldb::SBEvent native_event = lldb_event->GetNativeObject();
  const lldb::SBBreakpoint lldbBreakpoint =
      lldb::SBBreakpoint::GetBreakpointFromEvent(native_event);
  return gcnew LLDBBreakpoint(lldbBreakpoint);
}

bool LLDBBreakpointApi::EventIsBreakpointEvent(SbEvent ^ sbEvent) {
  LLDBEvent ^ lldb_event = safe_cast<LLDBEvent ^>(sbEvent);
  const lldb::SBEvent native_event = lldb_event->GetNativeObject();
  return lldb::SBBreakpoint::EventIsBreakpointEvent(native_event);
}

}  // namespace DebugEngine
}  // namespace YetiVSI

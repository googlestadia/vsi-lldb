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

#include "LLDBBreakpoint.h"

#include <msclr\marshal_cppstd.h>

#include "LLDBBreakpointLocation.h"
#include "lldb/API/SBStringList.h"

namespace YetiVSI {
namespace DebugEngine {

LLDBBreakpoint::LLDBBreakpoint(lldb::SBBreakpoint breakpoint) {
  breakpoint_ = MakeUniquePtr<lldb::SBBreakpoint>(breakpoint);
}

void LLDBBreakpoint::SetEnabled(bool enabled) {
  breakpoint_->SetEnabled(enabled);
}

uint32_t LLDBBreakpoint::GetNumLocations() {
  // Convert from size_t to a 32-bit return value as LLDB uses 32-bit for the
  // GetLocationAtIndex API.
  return (uint32_t)breakpoint_->GetNumLocations();
}

SbBreakpointLocation ^ LLDBBreakpoint::GetLocationAtIndex(uint32_t index) {
  lldb::SBBreakpointLocation location = breakpoint_->GetLocationAtIndex(index);
  if (!location.IsValid()) {
    return nullptr;
  }
  return gcnew LLDBBreakpointLocation(location);
}

SbBreakpointLocation ^ LLDBBreakpoint::FindLocationById(int32_t id) {
  auto location = breakpoint_->FindLocationByID(id);
  if (!location.IsValid()) {
    return nullptr;
  }
  return gcnew LLDBBreakpointLocation(location);
}

uint32_t LLDBBreakpoint::GetHitCount() { return breakpoint_->GetHitCount(); }

int LLDBBreakpoint::GetId() { return breakpoint_->GetID(); }

void LLDBBreakpoint::SetIgnoreCount(unsigned int ignoreCount) {
  breakpoint_->SetIgnoreCount(ignoreCount);
}

void LLDBBreakpoint::SetOneShot(bool isOneShot) {
  breakpoint_->SetOneShot(isOneShot);
}

void LLDBBreakpoint::SetCondition(System::String ^ condition) {
  breakpoint_->SetCondition(
      msclr::interop::marshal_as<std::string>(condition).c_str());
}

void LLDBBreakpoint::SetCommandLineCommands(
    System::Collections::Generic::List<System::String ^> ^ commands) {
  lldb::SBStringList sbCommands;
  for (int i = 0; i < commands->Count; ++i) {
    System::String ^ command = commands[i];
    std::string commandString =
        msclr::interop::marshal_as<std::string>(command);
    sbCommands.AppendString(commandString.c_str());
  }
  breakpoint_->SetCommandLineCommands(sbCommands);
}

}  // namespace DebugEngine
}  // namespace YetiVSI

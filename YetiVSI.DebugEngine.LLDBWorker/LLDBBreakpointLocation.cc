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

#include "string.h"
#include "LLDBBreakpointLocation.h"

#include <lldb/API/SBProcess.h>
#include <lldb/API/SBThread.h>
#include <msclr\marshal_cppstd.h>

#include "LLDBAddress.h"
#include "LLDBBreakpoint.h"
#include "lldb/API/SBAddress.h"
#include "lldb-eval/api.h"

namespace YetiVSI {
namespace DebugEngine {

LLDBBreakpointLocation::LLDBBreakpointLocation(
    lldb::SBBreakpointLocation location) {
  breakpointLocation_ = MakeUniquePtr<lldb::SBBreakpointLocation>(location);
}

void LLDBBreakpointLocation::SetEnabled(bool enabled) {
  breakpointLocation_->SetEnabled(enabled);
}

SbBreakpoint ^ LLDBBreakpointLocation::GetBreakpoint() {
  lldb::SBBreakpoint breakpoint = breakpointLocation_->GetBreakpoint();
  if (!breakpoint.IsValid()) {
    return nullptr;
  }
  return gcnew LLDBBreakpoint(breakpoint);
}

int LLDBBreakpointLocation::GetId() { return breakpointLocation_->GetID(); }

unsigned long long LLDBBreakpointLocation::GetLoadAddress() {
  return breakpointLocation_->GetLoadAddress();
}

SbAddress ^ LLDBBreakpointLocation::GetAddress() {
  lldb::SBAddress address = breakpointLocation_->GetAddress();
  if (address.IsValid()) {
    return gcnew LLDBAddress(address);
  } else {
    return nullptr;
  }
}

bool LldbEvalCallback(void* baton, lldb::SBProcess& process,
                      lldb::SBThread& thread,
                      lldb::SBBreakpointLocation& location) {
  const std::string* condition = static_cast<std::string*>(baton);
  lldb::SBError error;
  auto result = lldb_eval::EvaluateExpression(thread.GetSelectedFrame(),
                                              condition->c_str(), error);
  if (error.Fail()) {
    return false;
  }
  return result.GetValueAsUnsigned(0);
}

void LLDBBreakpointLocation::SetCondition(System::String^ condition) {
  auto conditionStr = msclr::interop::marshal_as<std::string>(condition);
  callbackBaton_ = MakeUniquePtr<std::string>(conditionStr);
  breakpointLocation_->SetCallback(LldbEvalCallback, (*callbackBaton_).Get());
}

void LLDBBreakpointLocation::SetIgnoreCount(unsigned int ignoreCount) {
  breakpointLocation_->SetIgnoreCount(ignoreCount);
}

uint32_t LLDBBreakpointLocation::GetHitCount() {
  return breakpointLocation_->GetHitCount();
}

}  // namespace DebugEngine
}  // namespace YetiVSI

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

#include "LLDBWatchpoint.h"

#include <msclr\marshal_cppstd.h>

namespace YetiVSI {
namespace DebugEngine {

LLDBWatchpoint::LLDBWatchpoint(lldb::SBWatchpoint watchpoint) {
  watchpoint_ = MakeUniquePtr<lldb::SBWatchpoint>(watchpoint);
}

int LLDBWatchpoint::GetId() { return watchpoint_->GetID(); }

unsigned int LLDBWatchpoint::GetHitCount() {
  return watchpoint_->GetHitCount();
}

void LLDBWatchpoint::SetEnabled(bool enabled) {
  watchpoint_->SetEnabled(enabled);
}

void LLDBWatchpoint::SetCondition(System::String ^ condition) {
  watchpoint_->SetCondition(
      msclr::interop::marshal_as<std::string>(condition).c_str());
}

void LLDBWatchpoint::SetIgnoreCount(unsigned int ignoreCount) {
  watchpoint_->SetIgnoreCount(ignoreCount);
}
}  // namespace DebugEngine
}  // namespace YetiVSI

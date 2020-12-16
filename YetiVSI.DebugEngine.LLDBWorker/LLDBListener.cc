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

#include "LLDBListener.h"

#include "LLDBEvent.h"
#include "LLDBObject.h"

namespace YetiVSI {
namespace DebugEngine {

LLDBListener::LLDBListener(lldb::SBListener listener) {
  listener_ = MakeUniquePtr<lldb::SBListener>(listener);
}

lldb::SBListener LLDBListener::GetNativeObject() { return *(*listener_).Get(); }

bool LLDBListener::WaitForEvent(
    uint32_t num_seconds,
    [System::Runtime::InteropServices::Out] SbEvent ^ % out_event) {
  lldb::SBEvent sbEvent;
  if (listener_->WaitForEvent(num_seconds, sbEvent)) {
    out_event = gcnew LLDBEvent(sbEvent);
    return true;
  } else {
    out_event = nullptr;
    return false;
  }
}

int64_t LLDBListener::GetId() {
  return GetSPAddress<lldb::SBListener>(GetNativeObject());
}

}  // namespace DebugEngine
}  // namespace YetiVSI
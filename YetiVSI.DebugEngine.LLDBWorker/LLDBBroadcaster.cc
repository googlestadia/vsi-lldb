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

#include "LLDBBroadcaster.h"
#include "LLDBListener.h"

namespace YetiVSI {
namespace DebugEngine {

LLDBBroadcaster::LLDBBroadcaster(lldb::SBBroadcaster broadcaster) {
  broadcaster_ = MakeUniquePtr<lldb::SBBroadcaster>(broadcaster);
}

unsigned LLDBBroadcaster::AddListener(SbListener ^ listener,
                                      unsigned eventMask) {
  LLDBListener ^ lldbListener = safe_cast<LLDBListener ^>(listener);
  return broadcaster_->AddListener(lldbListener->GetNativeObject(), eventMask);
}

unsigned LLDBBroadcaster::RemoveListener(SbListener ^ listener,
                                         unsigned eventMask) {
  LLDBListener ^ lldbListener = safe_cast<LLDBListener ^>(listener);
  return broadcaster_->RemoveListener(lldbListener->GetNativeObject(),
                                      eventMask);
}

}  // namespace DebugEngine
}  // namespace YetiVSI

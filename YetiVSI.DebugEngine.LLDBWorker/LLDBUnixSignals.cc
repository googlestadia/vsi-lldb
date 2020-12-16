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

#pragma managed(on)

#include "LLDBUnixSignals.h"

namespace YetiVSI {
namespace DebugEngine {

LLDBUnixSignals::LLDBUnixSignals(lldb::SBUnixSignals signals) {
  signals_ = MakeUniquePtr<lldb::SBUnixSignals>(signals);
}

bool LLDBUnixSignals::GetShouldStop(int32_t signalNumber) {
  return signals_->GetShouldStop(signalNumber);
}

bool LLDBUnixSignals::SetShouldStop(int32_t signalNumber, bool value) {
  return signals_->SetShouldStop(signalNumber, value);
}

}  // namespace DebugEngine
}  // namespace YetiVSI
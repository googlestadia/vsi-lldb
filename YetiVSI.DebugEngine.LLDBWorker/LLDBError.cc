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

#include "LLDBError.h"

namespace YetiVSI {
namespace DebugEngine {

LLDBError::LLDBError(lldb::SBError error) {
  error_ = MakeUniquePtr<lldb::SBError>(error);
}

bool LLDBError::Fail() { return error_->Fail(); }

bool LLDBError::Success() { return error_->Success(); }

uint32_t LLDBError::GetError() { return error_->GetError(); }

System::String ^ LLDBError::GetCString() {
  return gcnew System::String(error_->GetCString());
}

}  // namespace DebugEngine
}  // namespace YetiVSI
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

#include "LLDBInstruction.h"

#include "LLDBAddress.h"
#include "LLDBTarget.h"

namespace YetiVSI {
namespace DebugEngine {

LLDBInstruction::LLDBInstruction(lldb::SBInstruction instruction) {
  instruction_ = MakeUniquePtr<lldb::SBInstruction>(instruction);
}


SbAddress ^ LLDBInstruction::GetAddress() {
  auto address = instruction_->GetAddress();
  if (address.IsValid())
  {
    return gcnew LLDBAddress(address);
  }
  return nullptr;
}


System::String ^ LLDBInstruction::GetOperands(SbTarget ^ target) {
  auto lldbTarget = safe_cast<LLDBTarget ^>(target)->GetNativeObject();
  return gcnew System::String(instruction_->GetOperands(lldbTarget));
}

System::String ^ LLDBInstruction::GetMnemonic(SbTarget ^ target) {
  auto lldbTarget = safe_cast<LLDBTarget ^>(target)->GetNativeObject();
  return gcnew System::String(instruction_->GetMnemonic(lldbTarget));
}

System::String ^ LLDBInstruction::GetComment(SbTarget ^ target) {
  auto lldbTarget = safe_cast<LLDBTarget ^>(target)->GetNativeObject();
  return gcnew System::String(instruction_->GetComment(lldbTarget));
}

size_t LLDBInstruction::GetByteSize() {
  return instruction_->GetByteSize();
}

}  // namespace DebugEngine
}  // namespace YetiVSI
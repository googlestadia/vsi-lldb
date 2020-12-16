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

#include "LLDBFunction.h"

#include "LLDBAddress.h"
#include "LLDBInstruction.h"
#include "LLDBTarget.h"
#include "LLDBType.h"

namespace YetiVSI {
namespace DebugEngine {

LLDBFunction::LLDBFunction(lldb::SBFunction function) {
  function_ = MakeUniquePtr<lldb::SBFunction>(function);
}

SbAddress ^ LLDBFunction::GetStartAddress() {
  lldb::SBAddress address = function_->GetStartAddress();
  if (address.IsValid()) {
    return gcnew LLDBAddress(address);
  }
  return nullptr;
}

SbAddress ^ LLDBFunction::GetEndAddress() {
  lldb::SBAddress address = function_->GetEndAddress();
  if (address.IsValid()) {
    return gcnew LLDBAddress(address);
  }
  return nullptr;
}

System::Collections::Generic::List<SbInstruction ^> ^
    LLDBFunction::GetInstructions(SbTarget ^ target) {
  auto lldbTarget = safe_cast<LLDBTarget ^>(target)->GetNativeObject();
  auto instructions = function_->GetInstructions(lldbTarget);
  uint32_t list_size = (uint32_t)instructions.GetSize();
  auto list = gcnew System::Collections::Generic::List<SbInstruction ^>(list_size);
  for (uint32_t i = 0; i < list_size; i++) {
    list->Add(gcnew LLDBInstruction(instructions.GetInstructionAtIndex(i)));
  }
  return list;
}

LanguageType LLDBFunction::GetLanguage() {
  lldb::LanguageType language = function_->GetLanguage();
  return GetLanguageType(language);
}

LanguageType LLDBFunction::GetLanguageType(lldb::LanguageType language) {
  switch (language) {
    case lldb::LanguageType::eLanguageTypeC:
      return LanguageType::C;
    case lldb::LanguageType::eLanguageTypeC11:
      return LanguageType::C11;
    case lldb::LanguageType::eLanguageTypeC89:
      return LanguageType::C89;
    case lldb::LanguageType::eLanguageTypeC99:
      return LanguageType::C99;
    case lldb::LanguageType::eLanguageTypeC_plus_plus:
      return LanguageType::C_PLUS_PLUS;
    case lldb::LanguageType::eLanguageTypeC_plus_plus_03:
      return LanguageType::C_PLUS_PLUS_03;
    case lldb::LanguageType::eLanguageTypeC_plus_plus_11:
      return LanguageType::C_PLUS_PLUS_11;
    case lldb::LanguageType::eLanguageTypeC_plus_plus_14:
      return LanguageType::C_PLUS_PLUS_14;
    case lldb::LanguageType::eLanguageTypeUnknown:
      // fall through
    default:
      return LanguageType::UNKNOWN;
  }
}

System::String ^ LLDBFunction::GetName() {
  return gcnew System::String(function_->GetName());
}

SbType ^ LLDBFunction::GetType() {
  lldb::SBType t = function_->GetType();
  if (!t.IsValid()) {
    return nullptr;
  }
  return gcnew LLDBType(t);
}

System::String ^ LLDBFunction::GetArgumentName(unsigned int index) {
  const char* name = function_->GetArgumentName(index);
  return name == nullptr ? nullptr : gcnew System::String(name);
}


}  // namespace DebugEngine
}  // namespace YetiVSI
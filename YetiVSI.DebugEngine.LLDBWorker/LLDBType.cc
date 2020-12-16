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

#include "LLDBType.h"

#include <msclr\marshal_cppstd.h>

#include "LLDBTypeList.h"
#include "LLDBTypeMember.h"

#include "lldb/API/SBType.h"

namespace YetiVSI {
namespace DebugEngine {

LLDBType::LLDBType(lldb::SBType type) {
  type_ = MakeUniquePtr<lldb::SBType>(type);
}

LldbApi::TypeFlags LLDBType::GetTypeFlags() {
  return (LldbApi::TypeFlags)type_->GetTypeFlags();
}

System::String ^ LLDBType::GetName() {
  return gcnew System::String(type_->GetName());
}

uint32_t LLDBType::GetNumberOfDirectBaseClasses() {
  return type_->GetNumberOfDirectBaseClasses();
}

SbTypeMember ^ LLDBType::GetDirectBaseClassAtIndex(uint32_t index) {
  auto baseClass = type_->GetDirectBaseClassAtIndex(index);
  if (!baseClass.IsValid()) {
    return nullptr;
  }
  return gcnew LLDBTypeMember(baseClass);
}

SbType ^ LLDBType::GetCanonicalType() {
  auto t = type_->GetCanonicalType();
  if (!t.IsValid()) {
    return nullptr;
  }
  return gcnew LLDBType(t);
}

SbTypeList ^ LLDBType::GetFunctionArgumentTypes() {
  lldb::SBTypeList type_list = type_->GetFunctionArgumentTypes();
  if (!type_list.IsValid()) {
    return nullptr;
  }
  return gcnew LLDBTypeList(type_list);
}

SbType ^ LLDBType::GetPointeeType() {
  lldb::SBType t = type_->GetPointeeType();
  if (!t.IsValid()) {
    return nullptr;
  }
  return gcnew LLDBType(t);
}

uint64_t LLDBType::GetByteSize() {
  return type_->GetByteSize();
}

lldb::SBType LLDBType::GetNativeObject() { return *(*type_).Get(); }

}  // namespace DebugEngine
}  // namespace YetiVSI

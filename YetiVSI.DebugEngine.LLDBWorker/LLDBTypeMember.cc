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

#include "LLDBTypeMember.h"

#include <msclr\marshal_cppstd.h>

#include "LLDBType.h"

#include "lldb/API/SBType.h"

namespace YetiVSI {
namespace DebugEngine {

LLDBTypeMember::LLDBTypeMember(lldb::SBTypeMember typeMember) {
  typeMember_ = MakeUniquePtr<lldb::SBTypeMember>(typeMember);
}

SbType ^ LLDBTypeMember::GetTypeInfo() {
  auto typeInfo = typeMember_->GetType();
  if (typeInfo.IsValid()) {
    return gcnew LLDBType(typeInfo);
  }
  return nullptr;
}

}  // namespace DebugEngine
}  // namespace YetiVSI

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

#include "LLDBCommandReturnObject.h"

#include <msclr\marshal_cppstd.h>

#include "lldb/API/SBCommandReturnObject.h"
#include "lldb/API/SBStream.h"


namespace YetiVSI {
namespace DebugEngine {

LLDBCommandReturnObject::LLDBCommandReturnObject(lldb::SBCommandReturnObject result) {
  result_ = MakeUniquePtr<lldb::SBCommandReturnObject>(result);
}

bool LLDBCommandReturnObject::IsValid() {
  return result_->IsValid();
}

bool LLDBCommandReturnObject::Succeeded() {
  return result_->Succeeded();
}

System::String ^ LLDBCommandReturnObject::GetOutput() {
  return gcnew System::String(result_->GetOutput());
}

System::String ^ LLDBCommandReturnObject::GetError() {
  return gcnew System::String(result_->GetError());
}

System::String ^ LLDBCommandReturnObject::GetDescription() {
  lldb::SBStream desc;
  result_->GetDescription(desc);
  return gcnew System::String(desc.GetData());
}

}  // namespace DebugEngine
}  // namespace YetiVSI

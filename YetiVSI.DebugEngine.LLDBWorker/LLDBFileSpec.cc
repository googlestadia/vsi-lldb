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

#include "LLDBFileSpec.h"

namespace YetiVSI {
namespace DebugEngine {

LLDBFileSpec::LLDBFileSpec(lldb::SBFileSpec file_spec) {
  file_spec_ = MakeUniquePtr<lldb::SBFileSpec>(file_spec);
}

System::String ^ LLDBFileSpec::GetFilename() {
  return gcnew System::String(file_spec_->GetFilename());
}

System::String ^ LLDBFileSpec::GetDirectory() {
  return gcnew System::String(file_spec_->GetDirectory());
}

lldb::SBFileSpec LLDBFileSpec::GetNativeObject() {
  return *(*file_spec_).Get();
}

}  // namespace DebugEngine
}  // namespace YetiVSI

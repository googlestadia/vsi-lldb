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

#include "LLDBLineEntry.h"

#include "lldb/API/SBLineEntry.h"

#include "LLDBAddress.h"
#include "LLDBFileSpec.h"

namespace YetiVSI {
namespace DebugEngine {

LLDBLineEntry::LLDBLineEntry(lldb::SBLineEntry line_entry) {
  line_entry_ = MakeUniquePtr<lldb::SBLineEntry>(line_entry);
}

System::String ^ LLDBLineEntry::GetFileName() {
  return gcnew System::String(line_entry_->GetFileSpec().GetFilename());
}

System::String ^ LLDBLineEntry::GetDirectory() {
  return gcnew System::String(line_entry_->GetFileSpec().GetDirectory());
}

uint32_t LLDBLineEntry::GetLine() { return line_entry_->GetLine(); }

uint32_t LLDBLineEntry::GetColumn() { return line_entry_->GetColumn(); }

SbAddress ^ LLDBLineEntry::GetStartAddress() {
  auto address = line_entry_->GetStartAddress();
  if (!address.IsValid()) {
    return nullptr;
  }
  return gcnew LLDBAddress(address);
}

SbFileSpec ^ LLDBLineEntry::GetFileSpec() {
  auto fileSpec = line_entry_->GetFileSpec();
  if (!fileSpec.IsValid()) {
    return nullptr;
  }
  return gcnew LLDBFileSpec(fileSpec);
}

}  // namespace DebugEngine
}  // namespace YetiVSI

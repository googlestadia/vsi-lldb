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

#include "LLDBSymbol.h"

#include "LLDBAddress.h"

namespace YetiVSI {
namespace DebugEngine {

LLDBSymbol::LLDBSymbol(lldb::SBSymbol symbol) {
  symbol_ = MakeUniquePtr<lldb::SBSymbol>(symbol);
}

SbAddress ^ LLDBSymbol::GetStartAddress() {
  lldb::SBAddress address = symbol_->GetStartAddress();
  if (address.IsValid()) {
    return gcnew LLDBAddress(address);
  }
  return nullptr;
}

SbAddress ^ LLDBSymbol::GetEndAddress() {
  lldb::SBAddress address = symbol_->GetEndAddress();
  if (address.IsValid()) {
    return gcnew LLDBAddress(address);
  }
  return nullptr;
}

System::String ^ LLDBSymbol::GetName() {
  return gcnew System::String(symbol_->GetName());
}

}  // namespace DebugEngine
}  // namespace YetiVSI
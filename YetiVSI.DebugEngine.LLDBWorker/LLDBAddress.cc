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

#include "LLDBAddress.h"

#include "LLDBFunction.h"
#include "LLDBLineEntry.h"
#include "LLDBSymbol.h"
#include "LLDBTarget.h"

namespace YetiVSI {
namespace DebugEngine {

LLDBAddress::LLDBAddress(lldb::SBAddress address) {
  address_ = MakeUniquePtr<lldb::SBAddress>(address);
}

int64_t LLDBAddress::GetId() { throw gcnew System::NotImplementedException(); }

SbLineEntry ^ LLDBAddress::GetLineEntry() {
  lldb::SBLineEntry lineEntry = address_->GetLineEntry();
  if (lineEntry.IsValid()) {
    return gcnew LLDBLineEntry(address_->GetLineEntry());
  } else {
    return nullptr;
  }
}

uint64_t LLDBAddress::GetLoadAddress(SbTarget ^ target) {
  LLDBTarget ^ lldbTarget = safe_cast<LLDBTarget ^>(target);
  lldb::SBTarget sbTarget = lldbTarget->GetNativeObject();
  return address_->GetLoadAddress(sbTarget);
}

SbFunction ^ LLDBAddress::GetFunction() {
  auto function = address_->GetFunction();
  if (!function.IsValid()) {
    return nullptr;
  }
  return gcnew LLDBFunction(function);
}

SbSymbol ^ LLDBAddress::GetSymbol() {
  auto symbol = address_->GetSymbol();
  if (!symbol.IsValid()) {
    return nullptr;
  }
  return gcnew LLDBSymbol(symbol);
}

lldb::SBAddress LLDBAddress::GetNativeObject() { return *(*address_).Get(); }

}  // namespace DebugEngine
}  // namespace YetiVSI

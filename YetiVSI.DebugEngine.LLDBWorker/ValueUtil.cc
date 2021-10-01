// Copyright 2021 Google LLC
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

#include "ValueUtil.h"

#include "lldb/API/SBType.h"
#include "lldb/API/SBValue.h"
#include "lldb/lldb-enumerations.h"

namespace YetiVSI {
namespace DebugEngine {

lldb::SBValue ConvertToDynamicValue(lldb::SBValue value) {
  lldb::SBValue originalValue = value;
  bool shouldDereference = false;

  // `SBValue::GetDynamicValue` works well for pointers, but not for
  // dereferenced types. In that case, take an address of the value first and
  // dereference it later.
  if (value.GetType().IsPolymorphicClass()) {
    value = value.AddressOf();
    if (!value.IsValid()) {
      return originalValue;
    }
    shouldDereference = true;
  }

  // When evaluating an expression, LLDB and lldb-eval return the result in
  // dereferenced form. Therefore we don't explicitly check references.
  if (value.GetType().IsPointerType() &&
      value.GetType().GetPointeeType().IsPolymorphicClass()) {
    value = value.GetDynamicValue(lldb::eDynamicDontRunTarget);
    if (!value.IsValid()) {
      return originalValue;
    }
  }

  if (shouldDereference) {
    value = value.Dereference();
    if (!value.IsValid()) {
      return originalValue;
    }
  }

  return value;
}

}  // namespace DebugEngine
}  // namespace YetiVSI

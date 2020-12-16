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

#include "ValueTypeUtil.h"

namespace YetiVSI {
namespace DebugEngine {

LldbApi::ValueType ToLldbApiValueType(lldb::ValueType value_type) {
  switch (value_type) {
    case lldb::ValueType::eValueTypeVariableGlobal:
      return LldbApi::ValueType::VariableGlobal;
    case lldb::ValueType::eValueTypeVariableStatic:
      return LldbApi::ValueType::VariableStatic;
    case lldb::ValueType::eValueTypeVariableArgument:
      return LldbApi::ValueType::VariableArgument;
    case lldb::ValueType::eValueTypeVariableLocal:
      return LldbApi::ValueType::VariableLocal;
    case lldb::ValueType::eValueTypeRegister:
      return LldbApi::ValueType::Register;
    case lldb::ValueType::eValueTypeRegisterSet:
      return LldbApi::ValueType::RegisterSet;
    case lldb::ValueType::eValueTypeConstResult:
      return LldbApi::ValueType::ConstResult;
    case lldb::ValueType::eValueTypeVariableThreadLocal:
      return LldbApi::ValueType::VariableThreadLocal;
    case lldb::ValueType::eValueTypeInvalid:
    // Fall-through
    default:
      return LldbApi::ValueType::Invalid;
  }
}

lldb::ValueType ToLldbValueType(LldbApi::ValueType value_type) {
  switch (value_type) {
    case LldbApi::ValueType::VariableGlobal:
      return lldb::ValueType::eValueTypeVariableGlobal;
    case LldbApi::ValueType::VariableStatic:
      return lldb::ValueType::eValueTypeVariableStatic;
    case LldbApi::ValueType::VariableArgument:
      return lldb::ValueType::eValueTypeVariableArgument;
    case LldbApi::ValueType::VariableLocal:
      return lldb::ValueType::eValueTypeVariableLocal;
    case LldbApi::ValueType::Register:
      return lldb::ValueType::eValueTypeRegister;
    case LldbApi::ValueType::RegisterSet:
      return lldb::ValueType::eValueTypeRegisterSet;
    case LldbApi::ValueType::ConstResult:
      return lldb::ValueType::eValueTypeConstResult;
    case LldbApi::ValueType::VariableThreadLocal:
      return lldb::ValueType::eValueTypeVariableThreadLocal;
    case LldbApi::ValueType::Invalid:
    // Fall-through
    default:
      return lldb::ValueType::eValueTypeInvalid;
  }
}

}  // namespace DebugEngine
}  // namespace YetiVSI

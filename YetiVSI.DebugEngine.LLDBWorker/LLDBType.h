/*
 * Copyright 2020 Google LLC
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#pragma once

#include "lldb/API/SBType.h"

#include "ManagedUniquePtr.h"

namespace YetiVSI {
namespace DebugEngine {

using namespace LldbApi;

// Stores LLDB's type object and acts as an interface for its API calls.
private
ref class LLDBType sealed : SbType {
 public:
  LLDBType(lldb::SBType);
  virtual ~LLDBType(){};
  virtual LldbApi::TypeFlags GetTypeFlags();
  virtual System::String ^ GetName();
  virtual uint32_t GetNumberOfDirectBaseClasses();
  virtual SbTypeMember ^ GetDirectBaseClassAtIndex(uint32_t index);
  virtual SbType ^ GetCanonicalType();
  virtual SbType ^ GetPointeeType();
  virtual SbTypeList ^ GetFunctionArgumentTypes();
  virtual uint64_t GetByteSize();

  lldb::SBType GetNativeObject();

 private:
  ManagedUniquePtr<lldb::SBType> ^ type_;
};

}  // namespace DebugEngine
}  // namespace YetiVSI

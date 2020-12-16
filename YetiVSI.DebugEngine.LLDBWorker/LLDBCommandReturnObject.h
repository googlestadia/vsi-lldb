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

#include "lldb/API/SBCommandReturnObject.h"

#include "ManagedUniquePtr.h"

namespace YetiVSI {
namespace DebugEngine {

using namespace LldbApi;

private
ref class LLDBCommandReturnObject sealed : SbCommandReturnObject {
 public:
  LLDBCommandReturnObject(lldb::SBCommandReturnObject result);
  virtual ~LLDBCommandReturnObject() {};

  virtual bool IsValid();

  virtual bool Succeeded();
  virtual System::String ^ GetOutput();
  virtual System::String ^ GetError();
  virtual System::String ^ GetDescription();

 private:
  ManagedUniquePtr<lldb::SBCommandReturnObject> ^ result_;
};

}  // namespace DebugEngine
}  // namespace YetiVSI

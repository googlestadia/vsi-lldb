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

#include "lldb/API/SBFunction.h"

#include "ManagedUniquePtr.h"

namespace YetiVSI {
namespace DebugEngine {

using namespace LldbApi;

// Stores LLDB's function object and acts as an interface for its API calls.
private
ref class LLDBFunction sealed : SbFunction {
 public:
  LLDBFunction(lldb::SBFunction);
  virtual ~LLDBFunction(){};

  virtual SbAddress ^ GetStartAddress();
  virtual SbAddress ^ GetEndAddress();
  virtual LanguageType GetLanguage();
  virtual System::Collections::Generic::List<SbInstruction ^> ^ GetInstructions(SbTarget ^ target);
  virtual System::String ^ GetName();
  virtual SbType ^ GetType();
  virtual System::String ^ GetArgumentName(unsigned int index);

  LanguageType GetLanguageType(lldb::LanguageType language);

 private:
  ManagedUniquePtr<lldb::SBFunction> ^ function_;
};

}  // namespace DebugEngine
}  // namespace YetiVSI

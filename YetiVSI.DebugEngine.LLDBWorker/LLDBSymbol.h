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

#include "lldb/API/SBThread.h"

#include "LLDBStackFrame.h"
#include "ManagedUniquePtr.h"

namespace YetiVSI {
namespace DebugEngine {

using namespace LldbApi;

// Stores LLDB's SBSymbol and acts as an interface for its API calls.
private
ref class LLDBSymbol sealed : SbSymbol {
 public:
  LLDBSymbol(lldb::SBSymbol);
  virtual ~LLDBSymbol(){};

  virtual SbAddress ^ GetStartAddress();
  virtual SbAddress ^ GetEndAddress();
  virtual System::String ^ GetName();

 private:
  ManagedUniquePtr<lldb::SBSymbol> ^ symbol_;
};

}  // namespace DebugEngine
}  // namespace YetiVSI

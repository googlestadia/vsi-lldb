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

#include "lldb/API/SBBreakpoint.h"

#include "ManagedUniquePtr.h"

namespace YetiVSI {
namespace DebugEngine {

using namespace LldbApi;

private
ref class LLDBBreakpoint : SbBreakpoint {
 public:
  LLDBBreakpoint(lldb::SBBreakpoint);
  virtual ~LLDBBreakpoint(){};
  virtual void SetEnabled(bool enabled);
  virtual uint32_t GetNumLocations();
  virtual SbBreakpointLocation ^ GetLocationAtIndex(uint32_t index);
  virtual SbBreakpointLocation ^ FindLocationById(int32_t id);
  virtual uint32_t GetHitCount();
  virtual int GetId();
  virtual void SetIgnoreCount(unsigned int ignoreCount);
  virtual void SetOneShot(bool isOneShot);
  virtual void SetCondition(System::String ^ condition);
  virtual void SetCommandLineCommands(
      System::Collections::Generic::List<System::String ^> ^ commands);

 private:
  ManagedUniquePtr<lldb::SBBreakpoint> ^ breakpoint_;
};

}  // namespace DebugEngine
}  // namespace YetiVSI

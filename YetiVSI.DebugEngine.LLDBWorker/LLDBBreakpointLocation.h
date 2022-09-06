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

#include "lldb/API/SBBreakpointLocation.h"

#include "ManagedUniquePtr.h"
#include <string>

namespace YetiVSI {
namespace DebugEngine {

using namespace LldbApi;

private
ref class LLDBBreakpointLocation : SbBreakpointLocation {
 public:
  LLDBBreakpointLocation(lldb::SBBreakpointLocation);
  virtual ~LLDBBreakpointLocation(){};
  virtual void SetEnabled(bool enabled);
  virtual SbBreakpoint ^ GetBreakpoint();
  virtual int GetId();
  virtual unsigned long long GetLoadAddress();
  virtual SbAddress ^ GetAddress();
  virtual void SetCondition(System::String ^ condition);
  virtual void SetIgnoreCount(unsigned int ignoreCount);
  virtual uint32_t GetHitCount();

 private:
  ManagedUniquePtr<lldb::SBBreakpointLocation> ^ breakpointLocation_;
  ManagedUniquePtr<std::string> ^ callbackBaton_;
};

}  // namespace DebugEngine
}  // namespace YetiVSI

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

#include "lldb/API/SBTarget.h"

#include "ManagedUniquePtr.h"

namespace YetiVSI {
namespace DebugEngine {

using namespace LldbApi;

// Interface for a debugger target.
private
ref class LLDBTarget sealed : SbTarget {
 public:
  LLDBTarget(lldb::SBTarget);
  virtual ~LLDBTarget(){};
  virtual SbProcess ^
      AttachToProcessWithID(SbListener ^ listener, uint64_t pid,
                            [System::Runtime::InteropServices::Out] SbError ^
                                % out_error);
  virtual SbBreakpoint ^
      BreakpointCreateByLocation(System::String ^ file, uint32_t line);
  virtual SbBreakpoint ^ BreakpointCreateByName(System::String ^ symbolName);
  virtual SbBreakpoint ^ BreakpointCreateByAddress(uint64_t address);
  virtual SbBreakpoint ^ FindBreakpointById(int32_t id);
  virtual bool BreakpointDelete(int32_t id);
  virtual int32_t GetNumModules();
  virtual SbModule ^ GetModuleAtIndex(int32_t index);
  virtual bool operator==(SbTarget ^ target);
  virtual int64_t GetId();
  virtual SbWatchpoint ^
      WatchAddress(int64_t address, uint64_t size, bool read, bool write,
                   [System::Runtime::InteropServices::Out] SbError ^
                       % out_error);
  virtual bool DeleteWatchpoint(int32_t watchId);
  virtual SbAddress ^ ResolveLoadAddress(uint64_t address);
  virtual System::Collections::Generic::List<SbInstruction ^> ^
      ReadInstructions(SbAddress ^ baseAddress, uint32_t count, System::String ^ flavor);

  virtual System::Collections::Generic::List<SbInstruction^>^
      GetInstructionsWithFlavor(SbAddress^ baseAddress,
                                array<unsigned char>^ buffer,
                                unsigned long long size,
                                System::String^ flavor);
  virtual SbProcess ^ LoadCore(System::String ^ corePath);
  virtual SbModule ^ AddModule(System::String ^ path, System::String ^ triple,
    System::String ^ uuid);
  virtual bool RemoveModule(SbModule ^ module);
  virtual SbError ^
      SetModuleLoadAddress(SbModule ^ module, int64_t sectionsOffset);
  virtual SbProcess ^ GetProcess();
  virtual SbBroadcaster ^ LLDBTarget::GetBroadcaster();

  // Get the underlying lldb object.
  lldb::SBTarget GetNativeObject();
 private:
  ManagedUniquePtr<lldb::SBTarget> ^ target_;
};

}  // namespace DebugEngine
}  // namespace YetiVSI

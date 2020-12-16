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

#include <memory>

#include "lldb/API/SBListener.h"
#include "lldb/API/SBProcess.h"

#include "ManagedUniquePtr.h"

namespace YetiVSI {
namespace DebugEngine {

using namespace LldbApi;

// Stores LLDB's SBProcess, acts as an interface to its LLDB API calls,
// and deals with listening for the process's LLDB events.
private
ref class LLDBProcess sealed : SbProcess {
 public:
  LLDBProcess(lldb::SBProcess);
  virtual ~LLDBProcess() {};

  virtual SbTarget ^ GetTarget();
  virtual int32_t GetNumThreads();
  virtual SbThread ^ GetThreadAtIndex(int32_t index);
  virtual SbThread ^ GetThreadById(uint64_t id);
  virtual SbThread ^ GetSelectedThread();
  virtual bool SetSelectedThreadById(uint64_t threadId);
  virtual bool Stop();
  virtual bool Continue();
  virtual bool Detach();
  virtual bool Kill();
  virtual int32_t GetUniqueId();
  virtual SbUnixSignals ^ GetUnixSignals();
  virtual size_t ReadMemory(uint64_t address,
    array<unsigned char> ^ buffer, size_t size,
    [System::Runtime::InteropServices::Out] SbError ^ % out_error);
  virtual size_t WriteMemory(uint64_t address,
    array<unsigned char> ^ buffer, size_t size,
    [System::Runtime::InteropServices::Out] SbError ^ % out_error);
  virtual SbError ^ GetMemoryRegionInfo(uint64_t address,
    [System::Runtime::InteropServices::Out] SbMemoryRegionInfo ^ % memory_region);

private:
  ManagedUniquePtr<lldb::SBProcess> ^ process_;
};

}  // namespace DebugEngine
}  // namespace YetiVSI

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

#pragma managed(on)

#include "LLDBProcess.h"

#include <msclr/marshal_cppstd.h>
#include <vector>

#include "lldb/API/SBError.h"
#include "lldb/API/SBEvent.h"
#include "lldb/API/SBMemoryRegionInfoList.h"
#include "lldb/API/SBStream.h"
#include "lldb/API/SBUnixSignals.h"

#include "LLDBBreakpoint.h"
#include "LLDBError.h"
#include "LLDBMemoryRegionInfo.h"
#include "LLDBModule.h"
#include "LLDBTarget.h"
#include "LLDBThread.h"
#include "LLDBUnixSignals.h"

#using < system.dll >

namespace YetiVSI {
namespace DebugEngine {

namespace {

void Log(System::String ^ message) {
  System::String ^ tagged_message =
      System::String::Format("LLDBProcess: {0}", message);
  System::Diagnostics::Debug::WriteLine(tagged_message);
}

}  // namespace

LLDBProcess::LLDBProcess(lldb::SBProcess process) {
  process_ = MakeUniquePtr<lldb::SBProcess>(process);
}

SbTarget ^ LLDBProcess::GetTarget() {
  lldb::SBTarget target = process_->GetTarget();
  if (target.IsValid()) {
    return gcnew LLDBTarget(target);
  }
  return nullptr;
}

int32_t LLDBProcess::GetNumThreads() { return process_->GetNumThreads(); }

SbThread ^ LLDBProcess::GetThreadAtIndex(int32_t index) {
  lldb::SBThread sb_thread = process_->GetThreadAtIndex(index);
  if (sb_thread.IsValid()) {
    return gcnew LLDBThread(sb_thread);
  }
  return nullptr;
}

SbThread ^ LLDBProcess::GetThreadById(uint64_t id) {
  lldb::SBThread sb_thread = process_->GetThreadByID(id);
  if (sb_thread.IsValid()) {
    return gcnew LLDBThread(sb_thread);
  }
  return nullptr;
}

bool LLDBProcess::Stop() {
  lldb::SBError error = process_->Stop();
  if (error.Fail()) {
    Log("Failed to stop process: " + gcnew System::String(error.GetCString()));
    return false;
  }
  Log("Stopped process");
  return true;
}

bool LLDBProcess::Continue() {
  lldb::SBError error = process_->Continue();
  if (error.Fail()) {
    Log("Failed to continue process: " +
        gcnew System::String(error.GetCString()));
    return false;
  }
  Log("Continued process");
  return true;
}

bool LLDBProcess::Detach() {
  lldb::SBError error = process_->Detach();
  if (error.Fail()) {
    Log("Failed to detach process: " +
        gcnew System::String(error.GetCString()));
    return false;
  }
  Log("Detached process");
  return true;
}

bool LLDBProcess::Kill() {
  lldb::SBError error = process_->Kill();
  if (error.Fail()) {
    Log("Failed to kill process: " + gcnew System::String(error.GetCString()));
    return false;
  }
  Log("Killed process");
  return true;
}

SbThread ^ LLDBProcess::GetSelectedThread() {
  lldb::SBThread current_thread = lldb::SBThread(process_->GetSelectedThread());
  if (current_thread.IsValid()) {
    return gcnew LLDBThread(current_thread);
  }
  Log("Current thread is not valid");
  return nullptr;
}

bool LLDBProcess::SetSelectedThreadById(uint64_t threadId) {
  return process_->SetSelectedThreadByID(threadId);
}

int32_t LLDBProcess::GetUniqueId() { return process_->GetUniqueID(); }

SbUnixSignals ^ LLDBProcess::GetUnixSignals() {
  auto signals = process_->GetUnixSignals();
  if (signals.IsValid()) {
    return gcnew LLDBUnixSignals(signals);
  }
  return nullptr;
}

size_t LLDBProcess::ReadMemory(uint64_t address,
    array<unsigned char> ^ buffer, size_t size,
    [System::Runtime::InteropServices::Out] SbError ^ % out_error) {
  pin_ptr<byte> pinnedBytes = &buffer[0];
  lldb::SBError error;
  auto bytesRead = process_->ReadMemory(address, pinnedBytes, size, error);
  out_error = gcnew LLDBError(error);
  return bytesRead;
}

size_t LLDBProcess::WriteMemory(uint64_t address,
  array<unsigned char> ^ buffer, size_t size,
  [System::Runtime::InteropServices::Out] SbError ^ % out_error) {
  pin_ptr<byte> pinnedBytes = &buffer[0];
  lldb::SBError error;
  auto bytesWrote = process_->WriteMemory(address, pinnedBytes, size, error);
  out_error = gcnew LLDBError(error);
  return bytesWrote;
}

SbError ^ LLDBProcess::GetMemoryRegionInfo(uint64_t address,
  [System::Runtime::InteropServices::Out] SbMemoryRegionInfo ^ % memory_region) {
  lldb::SBMemoryRegionInfo sb_memory_region;
  lldb::SBError error = process_->GetMemoryRegionInfo(address, sb_memory_region);
  memory_region = gcnew LLDBMemoryRegionInfo(sb_memory_region);
  return gcnew LLDBError(error);
}

SbError ^ LLDBProcess::SaveCore(System::String ^ dumpPath) {
  std::string file_name = msclr::interop::marshal_as<std::string>(dumpPath);
  lldb::SBError error = process_->SaveCore(file_name.c_str());
  return gcnew LLDBError(error);
}

}  // namespace DebugEngine
}  // namespace YetiVSI

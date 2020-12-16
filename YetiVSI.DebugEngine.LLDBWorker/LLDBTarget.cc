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

#include "LLDBTarget.h"

#include <msclr/marshal_cppstd.h>

#include "lldb/API/SBProcess.h"

#include "LLDBAddress.h"
#include "LLDBBreakpoint.h"
#include "LLDBError.h"
#include "LLDBInstruction.h"
#include "LLDBListener.h"
#include "LLDBModule.h"
#include "LLDBObject.h"
#include "LLDBProcess.h"
#include "LLDBWatchpoint.h"

namespace YetiVSI {
namespace DebugEngine {

LLDBTarget::LLDBTarget(lldb::SBTarget target) {
  target_ = MakeUniquePtr<lldb::SBTarget>(target);
}

SbProcess ^ LLDBTarget::AttachToProcessWithID(
                SbListener ^ listener, uint64_t pid,
                [System::Runtime::InteropServices::Out] SbError ^ % out_error) {
  LLDBListener ^ lldbListener = safe_cast<LLDBListener ^>(listener);
  lldb::SBListener sbListener = lldbListener->GetNativeObject();
  lldb::SBError error;
  lldb::SBProcess process =
      target_->AttachToProcessWithID(sbListener, pid, error);
  out_error = gcnew LLDBError(error);
  if (!process.IsValid()) {
    return nullptr;
  }
  return gcnew LLDBProcess(process);
}

SbBreakpoint ^ LLDBTarget::BreakpointCreateByLocation(System::String ^ file,
                                                      uint32_t line) {
  auto name = msclr::interop::marshal_as<std::string>(file);
  lldb::SBBreakpoint breakpoint =
      target_->BreakpointCreateByLocation(name.c_str(), line);
  if (!breakpoint.IsValid()) {
    return nullptr;
  }
  return gcnew LLDBBreakpoint(breakpoint);
}

SbBreakpoint ^ LLDBTarget::BreakpointCreateByName(System::String ^ symbolName) {
  auto name = msclr::interop::marshal_as<std::string>(symbolName);
  lldb::SBBreakpoint breakpoint = target_->BreakpointCreateByName(name.c_str());
  if (!breakpoint.IsValid()) {
    return nullptr;
  }
  return gcnew LLDBBreakpoint(breakpoint);
}

SbBreakpoint ^ LLDBTarget::BreakpointCreateByAddress(uint64_t address) {
  lldb::SBBreakpoint breakpoint = target_->BreakpointCreateByAddress(address);
  if (!breakpoint.IsValid()) {
    return nullptr;
  }
  return gcnew LLDBBreakpoint(breakpoint);
}

SbBreakpoint ^ LLDBTarget::FindBreakpointById(int32_t id) {
  auto breakpoint = target_->FindBreakpointByID(id);
  if (!breakpoint.IsValid()) {
    return nullptr;
  }
  return gcnew LLDBBreakpoint(breakpoint);
}

bool LLDBTarget::BreakpointDelete(int32_t id) {
  return target_->BreakpointDelete(id);
}

bool LLDBTarget::operator==(SbTarget ^ target) {
  LLDBTarget ^ lldbTarget = safe_cast<LLDBTarget ^>(target);
  lldb::SBTarget lhs = GetNativeObject();
  lldb::SBTarget rhs = lldbTarget->GetNativeObject();
  return lhs == rhs;
}

int32_t LLDBTarget::GetNumModules() { return target_->GetNumModules(); }

SbModule ^ LLDBTarget::GetModuleAtIndex(int32_t index) {
  auto module = target_->GetModuleAtIndex(index);
  return module.IsValid() ? gcnew LLDBModule(module, *(*target_)) : nullptr;
}

int64_t LLDBTarget::GetId() {
  return GetSPAddress<lldb::SBTarget>(GetNativeObject());
}

lldb::SBTarget LLDBTarget::GetNativeObject() { return *(*target_).Get(); }

SbWatchpoint ^
    LLDBTarget::WatchAddress(int64_t address, uint64_t size, bool read,
                             bool write,
                             [System::Runtime::InteropServices::Out] SbError ^
                                 % out_error) {
  lldb::SBError error;
  lldb::SBWatchpoint watchpoint =
      target_->WatchAddress(address, size, read, write, error);
  out_error = gcnew LLDBError(error);
  if (!watchpoint.IsValid()) {
    return nullptr;
  }
  return gcnew LLDBWatchpoint(watchpoint);
}

bool LLDBTarget::DeleteWatchpoint(int32_t watchId) {
  return target_->DeleteWatchpoint(watchId);
}

SbAddress ^ LLDBTarget::ResolveLoadAddress(uint64_t address) {
  return gcnew LLDBAddress(target_->ResolveLoadAddress(address));
}

System::Collections::Generic::List<SbInstruction ^> ^
    LLDBTarget::ReadInstructions(SbAddress ^ baseAddress, uint32_t count, System::String ^ flavor) {
  auto lldbAddress = safe_cast<LLDBAddress ^>(baseAddress)->GetNativeObject();
  msclr::interop::marshal_context context;
  auto flavorCStr = context.marshal_as<const char*>(flavor);
  auto instructions = target_->ReadInstructions(lldbAddress, count, flavorCStr);
  uint32_t list_size = (uint32_t)instructions.GetSize();
  auto list =
      gcnew System::Collections::Generic::List<SbInstruction ^>(list_size);
  for (uint32_t i = 0; i < list_size; i++) {
    list->Add(gcnew LLDBInstruction(instructions.GetInstructionAtIndex(i)));
  }
  return list;
}

SbProcess ^ LLDBTarget::LoadCore(System::String ^ corePath) {
  auto process = target_->LoadCore(
      msclr::interop::marshal_as<std::string>(corePath).c_str());
  if (!process.IsValid()) {
    return nullptr;
  }
  return gcnew LLDBProcess(process);
}

LldbApi::SbModule ^ LLDBTarget::AddModule(System::String ^ path,
                                          System::String ^ triple,
                                          System::String ^ uuid) {
  msclr::interop::marshal_context context;
  auto pathCStr = context.marshal_as<const char*>(path);
  auto tripleCStr = context.marshal_as<const char*>(triple);
  auto uuidCStr = context.marshal_as<const char*>(uuid);
  auto module = target_->AddModule(pathCStr, tripleCStr, uuidCStr);
  return module.IsValid() ? gcnew LLDBModule(module, *(*target_)) : nullptr;
}

bool LLDBTarget::RemoveModule(SbModule ^ module) {
  auto lldbModule = safe_cast<LLDBModule ^>(module);
  return target_->RemoveModule(lldbModule->GetNativeObject());
}

SbError ^ LLDBTarget::SetModuleLoadAddress(SbModule ^ module,
                                           int64_t sectionsOffset) {
  auto lldbModule = safe_cast<LLDBModule ^>(module);
  auto error = target_->SetModuleLoadAddress(lldbModule->GetNativeObject(),
                                             sectionsOffset);
  return gcnew LLDBError(error);
}

SbProcess ^ LLDBTarget::GetProcess() {
  auto process = target_->GetProcess();
  if (!process.IsValid()) {
    return nullptr;
  }
  return gcnew LLDBProcess(process);
}

}  // namespace DebugEngine
}  // namespace YetiVSI

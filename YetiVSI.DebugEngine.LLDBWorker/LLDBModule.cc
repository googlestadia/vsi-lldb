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

#include "LLDBModule.h"

#include <msclr/marshal_cppstd.h>

#include "lldb/API/SBSection.h"

#include "LLDBAddress.h"
#include "LLDBFileSpec.h"
#include "LLDBObject.h"
#include "LLDBSection.h"

#using < system.dll>

namespace YetiVSI {
namespace DebugEngine {

namespace {

void Log(System::String ^ message) {
  System::String ^ tagged_message =
      System::String::Format("LLDBModule: {0}", message);
  System::Diagnostics::Debug::WriteLine(tagged_message);
}

bool FindFirstCodeSection(lldb::SBSection section, lldb::SBSection* out) {
  if (section.GetSectionType() == lldb::eSectionTypeCode) {
    *out = section;
    return true;
  }
  size_t num_sub_sections = section.GetNumSubSections();
  for (size_t i = 0; i < num_sub_sections; i++) {
    if (FindFirstCodeSection(section.GetSubSectionAtIndex(i), out)) {
      return true;
    }
  }
  return false;
}

}  // namespace

LLDBModule::LLDBModule(lldb::SBModule module, lldb::SBTarget target) {
  module_ = MakeUniquePtr<lldb::SBModule>(module);
  target_ = MakeUniquePtr<lldb::SBTarget>(target);

  // Find the target's architecture, the first item in the triple
  try {
    architecture_ = (gcnew System::String(module.GetTriple()))->Split('-')[0];
  } catch (System::IndexOutOfRangeException ^) {
    Log("Could not determine architecture of " +
        gcnew System::String(module.GetPlatformFileSpec().GetFilename()));
  }

  // Find the code section (name should be .text, __TEXT, etc)
  lldb::SBSection section;
  size_t num_sections = module.GetNumSections();
  for (size_t i = 0; i < num_sections; ++i) {
    if (FindFirstCodeSection(module.GetSectionAtIndex(i), &section)) {
      code_section_ = MakeUniquePtr<lldb::SBSection>(section);
      break;
    }
  }

  if (code_section_ == nullptr) {
    Log("Module " +
        gcnew System::String(module.GetPlatformFileSpec().GetFilename()) +
        " does not have a code section");
  }
}

SbFileSpec ^ LLDBModule::GetFileSpec() {
  auto file_spec = module_->GetFileSpec();
  if (file_spec.IsValid()) {
    return gcnew LLDBFileSpec(file_spec);
  }
  return nullptr;
}

SbFileSpec ^ LLDBModule::GetPlatformFileSpec() {
  auto file_spec = module_->GetPlatformFileSpec();
  if (file_spec.IsValid()) {
    return gcnew LLDBFileSpec(file_spec);
  }
  return nullptr;
}

bool LLDBModule::SetPlatformFileSpec(SbFileSpec ^ fileSpec) {
  auto lldbFileSpec = safe_cast<LLDBFileSpec ^>(fileSpec);
  return module_->SetPlatformFileSpec(lldbFileSpec->GetNativeObject());
}

SbFileSpec ^ LLDBModule::GetSymbolFileSpec() {
  auto file_spec = module_->GetSymbolFileSpec();
  if (file_spec.IsValid()) {
    return gcnew LLDBFileSpec(file_spec);
  }
  return nullptr;
}

uint64_t LLDBModule::GetCodeLoadAddress() {
  if (code_section_ == nullptr) {
    return 0;
  }
  return code_section_->GetLoadAddress(*(*target_));
}

SbAddress^ LLDBModule::GetObjectFileHeaderAddress() {
  auto address = module_->GetObjectFileHeaderAddress();
  if (address.IsValid())
  {
    return gcnew LLDBAddress(address);
  }
  return nullptr;
}

uint64_t LLDBModule::GetCodeSize() {
  if (code_section_ == nullptr) {
    return 0;
  }
  return code_section_->GetByteSize();
}

bool LLDBModule::Is64Bit() { return architecture_->Equals("x86_64"); }

bool LLDBModule::HasSymbols() { return module_->GetNumSymbols() != 0; }

bool LLDBModule::HasCompileUnits() {
  return module_->GetNumCompileUnits() != 0;
}

uint32_t LLDBModule::GetNumCompileUnits() {
  return module_->GetNumCompileUnits();
}

System::String ^ LLDBModule::GetUUIDString() {
  return gcnew System::String(module_->GetUUIDString());
}

LldbApi::SbSection ^ LLDBModule::FindSection(System::String ^ name) {
  auto section = module_->FindSection(
      msclr::interop::marshal_as<std::string>(name).c_str());
  return section.IsValid() ? gcnew LLDBSection(section) : nullptr;
}

uint64_t LLDBModule::GetNumSections() { return module_->GetNumSections(); }

LldbApi::SbSection ^ LLDBModule::GetSectionAtIndex(uint64_t index) {
  auto section = module_->GetSectionAtIndex(index);
  return section.IsValid() ? gcnew LLDBSection(section) : nullptr;
}

bool LLDBModule::EqualTo(SbModule ^ otherModule) {
  auto other = dynamic_cast<LLDBModule ^>(otherModule);
  return other != nullptr && **module_ == **(other->module_);
}

lldb::SBModule LLDBModule::GetNativeObject() { return *(*module_).Get(); }

}  // namespace DebugEngine
}  // namespace YetiVSI

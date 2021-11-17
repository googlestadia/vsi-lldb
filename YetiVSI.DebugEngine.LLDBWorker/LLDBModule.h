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

#include "lldb/API/SBModule.h"
#include "lldb/API/SBStream.h"

#include "ManagedUniquePtr.h"

namespace YetiVSI {
namespace DebugEngine {

using namespace LldbApi;

private
ref class LLDBModule sealed : SbModule {
 public:
  LLDBModule(lldb::SBModule, lldb::SBTarget);

  virtual SbFileSpec ^ GetFileSpec();
  virtual SbFileSpec ^ GetPlatformFileSpec();
  virtual bool SetPlatformFileSpec(SbFileSpec ^ fileSpec);
  virtual SbFileSpec ^ GetSymbolFileSpec();
  virtual uint64_t GetCodeLoadAddress();
  virtual SbAddress^ GetObjectFileHeaderAddress();
  virtual uint64_t GetCodeSize();
  virtual bool Is64Bit();
  virtual bool HasSymbols();
  virtual bool HasCompileUnits();
  virtual uint32_t GetNumCompileUnits();
  virtual System::String ^ GetUUIDString();
  virtual System::String^ GetTriple();
  virtual SbSection ^ FindSection(System::String ^ name);
  virtual uint64_t GetNumSections();
  virtual SbSection ^ GetSectionAtIndex(uint64_t index);
  virtual bool EqualTo(SbModule ^ otherModule);

  // Get the underlying lldb object.
  lldb::SBModule GetNativeObject();

 private:
  ManagedUniquePtr<lldb::SBModule> ^ module_;
  ManagedUniquePtr<lldb::SBTarget> ^ target_;
  ManagedUniquePtr<lldb::SBSection> ^ code_section_;
  System::String ^ architecture_;
};

}  // namespace DebugEngine
}  // namespace YetiVSI

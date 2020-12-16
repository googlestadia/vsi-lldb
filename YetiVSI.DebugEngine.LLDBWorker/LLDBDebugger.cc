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

#include "LLDBDebugger.h"

#include "lldb/API/SBEvent.h"
#include "lldb/API/SBListener.h"
#include "lldb/API/SBProcess.h"
#include "lldb/API/SBStream.h"
#include "lldb/API/SBStructuredData.h"
#include "lldb/API/SBTarget.h"

#include <msclr\marshal_cppstd.h>

#include "LLDBCommandInterpreter.h"
#include "LLDBPlatform.h"
#include "LLDBTarget.h"

#using < system.dll >

namespace YetiVSI {
namespace DebugEngine {
namespace {

void Log(System::String ^ message) {
  System::String ^ tagged_message =
      System::String::Format("LLDB: {0}", message);
  System::Diagnostics::Trace::WriteLine(tagged_message);
}

void LoggingCallback(const char* message, void*) {
  Log(gcnew System::String(message));
}
}  // namespace

LLDBDebugger::LLDBDebugger(bool sourceInitFiles) {
  // Make sure LLDB is initialized before creating the debugger.
  // Calling initialize multiple times has no side effects.
  lldb::SBDebugger::Initialize();

  // We can't store a non-managed class (aka LLDB objects) as members of a
  // managed class, and LLDB only provides a way to create an SBDebugger
  // object on the stack. So create the object on the stack, and then create
  // a new object on the heap from a copy of the object on the stack.
  debugger_ = MakeUniquePtr<lldb::SBDebugger>(
      lldb::SBDebugger::Create(sourceInitFiles, LoggingCallback, nullptr));
}

void LLDBDebugger::SetAsync(bool async) { debugger_->SetAsync(async); }

void LLDBDebugger::SkipLLDBInitFiles(bool skip) {
  debugger_->SkipLLDBInitFiles(skip);
}

SbCommandInterpreter ^ LLDBDebugger::GetCommandInterpreter() {
  lldb::SBCommandInterpreter interpreter = debugger_->GetCommandInterpreter();
  if (!interpreter.IsValid()) {
    return nullptr;
  }
  return gcnew LLDBCommandInterpreter(interpreter);
}

SbTarget ^ LLDBDebugger::CreateTarget(System::String ^ filename) {
  auto name = msclr::interop::marshal_as<std::string>(filename);
  lldb::SBTarget target = debugger_->CreateTarget(name.c_str());
  if (!target.IsValid()) {
    return nullptr;
  }
  return gcnew LLDBTarget(target);
}

bool LLDBDebugger::DeleteTarget(SbTarget ^ target) {
  LLDBTarget ^ lldbTarget = safe_cast<LLDBTarget ^>(target);
  lldb::SBTarget sbTarget = lldbTarget->GetNativeObject();
  return debugger_->DeleteTarget(sbTarget);
}

void LLDBDebugger::SetSelectedPlatform(SbPlatform ^ platform) {
  LLDBPlatform ^ lldbPlatform = safe_cast<LLDBPlatform ^>(platform);
  lldb::SBPlatform sbPlatform = lldbPlatform->GetNativeObject();
  debugger_->SetSelectedPlatform(sbPlatform);
}

SbPlatform ^ LLDBDebugger::GetSelectedPlatform() {
  lldb::SBPlatform platform = debugger_->GetSelectedPlatform();
  if (!platform.IsValid()) {
    return nullptr;
  }
  return gcnew LLDBPlatform(platform);
}

bool LLDBDebugger::EnableLog(
    System::String ^ channel,
    System::Collections::Generic::List<System::String ^> ^ types) {
  auto stdChannel = msclr::interop::marshal_as<std::string>(channel);

  // Convert types to a vector of std::string to store the c strings locally.
  std::vector<std::string> stdTypes;
  for each (auto type in types) {
    auto stdType = msclr::interop::marshal_as<std::string>(type);
    stdTypes.push_back(stdType);
  }

  // Build the raw char* array that LLDB expects.
  std::unique_ptr<const char* []> rawTypes(new const char*[stdTypes.size() + 1]);
  for (size_t i = 0; i < stdTypes.size(); i++) {
    rawTypes[i] = stdTypes[i].c_str();
  }
  rawTypes[stdTypes.size()] = nullptr;
  return debugger_->EnableLog(stdChannel.c_str(), rawTypes.get());
}

bool LLDBDebugger::IsPlatformAvailable(System::String ^ platformName) {
  auto name = msclr::interop::marshal_as<std::string>(platformName);
  uint32_t platformsCount = debugger_->GetNumAvailablePlatforms();
  std::string currentPlatformName;
  for (uint32_t i = 0; i < platformsCount; ++i) {
    lldb::SBStructuredData platform =
        debugger_->GetAvailablePlatformInfoAtIndex(i);
    lldb::SBStructuredData nameData = platform.GetValueForKey("name");
    // +1 below is needed because GetStringValue explicitly writes the null terminator.
    size_t size = nameData.GetStringValue(nullptr, 0) + 1;
    currentPlatformName.resize(size);
    nameData.GetStringValue(const_cast<char*>(currentPlatformName.data()), size);
    if (name.compare(currentPlatformName.c_str()) == 0) {
      return true;
    }
  }
  return false;
}

}  // namespace DebugEngine
}  // namespace YetiVSI

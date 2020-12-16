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

#include "lldb/API/SBDebugger.h"

#include "LLDBProcess.h"
#include "ManagedUniquePtr.h"

namespace YetiVSI {
namespace DebugEngine {

using namespace LldbApi;

// Represents an object which will interact with debugger C++ APIs.
//
// A placeholder 'managed' C++ class (compiled as .NET, can be used easily in
// C#). Certain 'native' C++ data types are not allowed in 'managed' code, so
// actual API calls may need to be done by a 'native' C++ class which this
// object references.
//
// Sample C# usage within the YetiVSI.DebugEngine namespace:
//     LLDBWorker worker = new LLDBWorker();
//     LLDBWorker.Initialize();
private
ref class LLDBDebugger sealed : SbDebugger {
 public:
  LLDBDebugger(bool sourceInitFiles);
  virtual ~LLDBDebugger(){};

  virtual void SetAsync(bool async);
  virtual void SkipLLDBInitFiles(bool skip);
  virtual SbCommandInterpreter ^ GetCommandInterpreter();
  virtual SbTarget ^ CreateTarget(System::String ^ filename);
  virtual bool DeleteTarget(SbTarget ^ target);
  virtual void SetSelectedPlatform(SbPlatform ^ platform);
  virtual SbPlatform ^ GetSelectedPlatform();
  virtual bool EnableLog(System::String ^ channel,
                         System::Collections::Generic::List<System::String ^> ^
                             types);
  virtual bool IsPlatformAvailable(System::String ^ platformName);

 private:
  ManagedUniquePtr<lldb::SBDebugger> ^ debugger_;
};

}  // namespace DebugEngine
}  // namespace YetiVSI

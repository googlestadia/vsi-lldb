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

#include "lldb/API/SBThread.h"

#include "LLDBStackFrame.h"
#include "ManagedUniquePtr.h"

namespace YetiVSI {
namespace DebugEngine {

using namespace LldbApi;

// Stores LLDB's SBThread and acts as an interface for its API calls.
private
ref class LLDBThread sealed : SbThread {
 public:
  LLDBThread(lldb::SBThread);
  virtual ~LLDBThread(){};

  virtual SbProcess ^ GetProcess();
  virtual System::String ^ GetName();
  virtual uint64_t GetThreadId();
  virtual System::String ^ GetStatus();
  virtual void StepInto();
  virtual void StepOver();
  virtual void StepOut();
  virtual void StepInstruction(bool step_over);
  virtual uint32_t GetNumFrames();
  virtual SbFrame ^ GetFrameAtIndex(uint32_t index);
  virtual StopReason GetStopReason();
  virtual uint64_t GetStopReasonDataAtIndex(uint32_t index);
  virtual uint32_t GetStopReasonDataCount();

 private:
  ManagedUniquePtr<lldb::SBThread> ^ thread_;
};

}  // namespace DebugEngine
}  // namespace YetiVSI

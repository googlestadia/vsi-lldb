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

#include "LLDBThread.h"

#include <msclr\marshal_cppstd.h>

#include "lldb/API/SBStream.h"

#include "LLDBError.h"
#include "LLDBProcess.h"

#using < system.dll >

namespace YetiVSI {
namespace DebugEngine {

namespace {

void Log(System::String ^ message) {
  System::String ^ tagged_message =
      System::String::Format("LLDBThread: {0}", message);
  System::Diagnostics::Debug::WriteLine(tagged_message);
}

}  // namespace

LLDBThread::LLDBThread(lldb::SBThread thread) {
  thread_ = MakeUniquePtr<lldb::SBThread>(thread);
}

SbProcess ^ LLDBThread::GetProcess() {
  lldb::SBProcess process = thread_->GetProcess();
  if (process.IsValid()) {
    return gcnew LLDBProcess(process);
  }
  return nullptr;
}

System::String ^ LLDBThread::GetName() {
  return gcnew System::String(thread_->GetName());
}

uint64_t LLDBThread::GetThreadId() { return thread_->GetThreadID(); }

System::String ^ LLDBThread::GetStatus() {
  lldb::SBStream status_stream;
  if (thread_->GetStatus(status_stream)) {
    return gcnew System::String(status_stream.GetData());
  } else {
    return nullptr;
  }
}

void LLDBThread::StepInto() { thread_->StepInto(); }

void LLDBThread::StepOver() { thread_->StepOver(); }

void LLDBThread::StepOut() { thread_->StepOut(); }

void LLDBThread::StepInstruction(bool step_over) {
  thread_->StepInstruction(step_over);
}

uint32_t LLDBThread::GetNumFrames() { return thread_->GetNumFrames(); }

SbFrame ^ LLDBThread::GetFrameAtIndex(uint32_t index) {
  lldb::SBFrame frame = thread_->GetFrameAtIndex(index);
  if (frame.IsValid()) {
    return gcnew LLDBStackFrame(frame);
  }
  return nullptr;
}

StopReason LLDBThread::GetStopReason() {
  switch (thread_->GetStopReason()) {
    case lldb::StopReason::eStopReasonNone:
      return StopReason::NONE;
    case lldb::StopReason::eStopReasonTrace:
      return StopReason::TRACE;
    case lldb::StopReason::eStopReasonBreakpoint:
      return StopReason::BREAKPOINT;
    case lldb::StopReason::eStopReasonWatchpoint:
      return StopReason::WATCHPOINT;
    case lldb::StopReason::eStopReasonSignal:
      return StopReason::SIGNAL;
    case lldb::StopReason::eStopReasonException:
      return StopReason::EXCEPTION;
    case lldb::StopReason::eStopReasonExec:
      return StopReason::EXEC;
    case lldb::StopReason::eStopReasonPlanComplete:
      return StopReason::PLAN_COMPLETE;
    case lldb::StopReason::eStopReasonThreadExiting:
      return StopReason::EXITING;
    case lldb::StopReason::eStopReasonInstrumentation:
      return StopReason::INSTRUMENTATION;
    case lldb::StopReason::eStopReasonInvalid:
      // fall-through
    default:
      return StopReason::INVALID;
  }
}

uint64_t LLDBThread::GetStopReasonDataAtIndex(uint32_t index) {
  return thread_->GetStopReasonDataAtIndex(index);
}

uint32_t LLDBThread::GetStopReasonDataCount() {
  // Convert from size_t to a 32-bit return value as LLDB uses 32-bit for the
  // GetStopReasonDataAtIndex API.
  return (uint32_t)thread_->GetStopReasonDataCount();
}

SbError ^ LLDBThread::JumpToLine(System::String ^ filePath, uint32_t line) {
  lldb::SBFileSpec fileSpec((msclr::interop::marshal_as<std::string>(
    filePath)).c_str(), true);
  auto error = thread_->JumpToLine(fileSpec, line);
  if (!error.IsValid()) {
    return nullptr;
  }
  return gcnew LLDBError(error);
}

}  // namespace DebugEngine
}  // namespace YetiVSI

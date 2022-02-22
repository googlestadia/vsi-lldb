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

#include "LLDBStackFrame.h"

#include "lldb/API/SBFunction.h"
#include "lldb/API/SBLanguageRuntime.h"
#include "lldb/API/SBLineEntry.h"
#include "lldb/API/SBProcess.h"
#include "lldb/API/SBThread.h"
#include "lldb/API/SBValue.h"
#include "lldb/API/SBValueList.h"

#include <msclr\marshal_cppstd.h>

#include "LLDBExpressionOptions.h"
#include "LLDBFunction.h"
#include "LLDBLineEntry.h"
#include "LLDBModule.h"
#include "LLDBSymbol.h"
#include "LLDBThread.h"
#include "LLDBValue.h"
#include "ValueTypeUtil.h"
#include "ValueUtil.h"

#using < system.dll >

namespace YetiVSI {
namespace DebugEngine {

namespace {

void Log(System::String ^ message) {
  System::String ^ tagged_message =
      System::String::Format("LLDBStackFrame: {0}", message);
  System::Diagnostics::Debug::WriteLine(tagged_message);
}

}  // namespace

LLDBStackFrame::LLDBStackFrame(lldb::SBFrame frame) {
  frame_ = MakeUniquePtr<lldb::SBFrame>(frame);
}

SbSymbol ^ LLDBStackFrame::GetSymbol() {
  lldb::SBSymbol symbol = frame_->GetSymbol();
  if (symbol.IsValid()) {
    return gcnew LLDBSymbol(symbol);
  }
  return nullptr;
}

System::String ^ LLDBStackFrame::GetFunctionName() {
  return gcnew System::String(frame_->GetFunctionName());
}

SbFunction ^ LLDBStackFrame::GetFunction() {
  return gcnew LLDBFunction(frame_->GetFunction());
}

System::Collections::Generic::List<SbValue ^> ^
    LLDBStackFrame::GetVariables(bool arguments, bool locals, bool statics,
                                 bool only_in_scope) {
  return BuildManagedValues(
      frame_->GetVariables(arguments, locals, statics, only_in_scope));
}

SbValue ^ LLDBStackFrame::GetValueForVariablePath(System::String ^ varPath) {
  auto value = frame_->GetValueForVariablePath(
      msclr::interop::marshal_as<std::string>(varPath).c_str());
  if (value.IsValid()) {
    // Try converting the result to dynamic type. That way the VSI extension
    // will be able to pick up the correct Natvis visualization.
    value = ConvertToDynamicValue(value);
    return gcnew LLDBValue(value);
  }
  return nullptr;
}

SbValue ^ LLDBStackFrame::FindValue(System::String ^ varName, ValueType value_type) {
  auto value = frame_->FindValue(msclr::interop::marshal_as<std::string>(varName).c_str(),
      ToLldbValueType(value_type));
  if (value.IsValid()) {
    return gcnew LLDBValue(value);
  }
  return nullptr;
}

System::Collections::Generic::List<SbValue ^> ^ LLDBStackFrame::GetRegisters() {
  return BuildManagedValues(frame_->GetRegisters());
}

System::Collections::Generic::List<SbValue ^> ^
    LLDBStackFrame::BuildManagedValues(lldb::SBValueList value_list) {
  uint32_t list_size = value_list.GetSize();
  auto values = gcnew System::Collections::Generic::List<SbValue ^>(list_size);
  for (uint32_t i = 0; i < list_size; i++) {
    values->Add(gcnew LLDBValue(value_list.GetValueAtIndex(i)));
  }
  return values;
}

SbModule ^ LLDBStackFrame::GetModule() {
  auto module = frame_->GetModule();
  if (module.IsValid()) {
    return gcnew LLDBModule(module,
                            frame_->GetThread().GetProcess().GetTarget());
  }
  return nullptr;
}

SbLineEntry ^ LLDBStackFrame::GetLineEntry() {
  lldb::SBLineEntry line_entry = frame_->GetLineEntry();
  if (line_entry.IsValid()) {
    return gcnew LLDBLineEntry(line_entry);
  }
  return nullptr;
}

SbThread ^ LLDBStackFrame::GetThread() {
  lldb::SBThread thread = frame_->GetThread();
  if (thread.IsValid()) {
    return gcnew LLDBThread(thread);
  }
  return nullptr;
}

uint64_t LLDBStackFrame::GetPC() { return frame_->GetPC(); }

bool LLDBStackFrame::SetPC(uint64_t addr) { return frame_->SetPC(addr); }

SbValue ^ LLDBStackFrame::EvaluateExpression(System::String ^ text,
                                             SbExpressionOptions ^ options) {
  auto lldbExpressionOptions = safe_cast<LLDBExpressionOptions ^>(options);
  auto value = frame_->EvaluateExpression(
      msclr::interop::marshal_as<std::string>(text).c_str(),
      lldbExpressionOptions->GetNativeObject());
  if (!value.IsValid()) {
    return nullptr;
  }
  // Try converting the result to dynamic type. That way the VSI extension will
  // be able to pick up the correct Natvis visualization.
  value = ConvertToDynamicValue(value);
  return gcnew LLDBValue(value);
}

lldb::SBFrame LLDBStackFrame::GetNativeObject() { return *(*frame_).Get(); }

}  // namespace DebugEngine
}  // namespace YetiVSI

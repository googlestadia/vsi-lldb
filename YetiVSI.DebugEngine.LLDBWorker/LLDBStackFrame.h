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

#include "lldb/API/SBBlock.h"
#include "lldb/API/SBFrame.h"
#include "lldb/API/SBFunction.h"
#include "lldb/API/SBLineEntry.h"
#include "lldb/API/SBStream.h"
#include "lldb/API/SBSymbol.h"
#include "lldb/API/SBTarget.h"

#include "ManagedUniquePtr.h"

namespace YetiVSI {
namespace DebugEngine {

using namespace LldbApi;

// Stores LLDB's frame object and acts as an interface for its API calls.
private
ref class LLDBStackFrame sealed : SbFrame {
 public:
  LLDBStackFrame(lldb::SBFrame);
  virtual ~LLDBStackFrame(){};

  virtual SbSymbol ^ GetSymbol();
  virtual System::String ^ GetFunctionName();
  virtual SbFunction ^ GetFunction();
  virtual System::Collections::Generic::List<SbValue ^> ^
      GetVariables(bool arguments, bool locals, bool statics,
                   bool only_in_scope);
  virtual SbValue ^ LLDBStackFrame::GetValueForVariablePath(System::String ^ varPath);
  virtual SbValue ^ FindValue(System::String ^ varName, ValueType value_type);
  virtual System::Collections::Generic::List<SbValue ^> ^
      GetRegisters();
  virtual SbModule ^ GetModule();
  virtual SbLineEntry ^ GetLineEntry();
  virtual SbThread ^ GetThread();
  virtual uint64_t GetPC();

  virtual SbValue ^ EvaluateExpression(System::String ^ text, SbExpressionOptions ^ options);

  lldb::SBFrame GetNativeObject();

 private:
  static System::Collections::Generic::List<SbValue ^> ^
      BuildManagedValues(lldb::SBValueList);

  ManagedUniquePtr<lldb::SBFrame> ^ frame_;

  property uint64_t ProgramCounter {
    uint64_t get() { return GetPC(); }
  }

  property uint64_t FramePointer {
    uint64_t get() { return frame_->GetFP(); }
  }

  property uint64_t StackPointer {
    uint64_t get() { return frame_->GetSP(); }
  }

  property uint64_t CanonFrameAddress {
    uint64_t get() { return frame_->GetCFA(); }
  }

  property System::String ^ Line {
    System::String ^ get() {
      lldb::SBStream desc;
      frame_->GetLineEntry().GetDescription(desc);
      return gcnew System::String(desc.GetData());
    }
  }

  property System::String ^ Function {
    System::String ^ get() {
      lldb::SBStream desc;
      frame_->GetFunction().GetDescription(desc);
      return gcnew System::String(desc.GetData());
    }
  }

  property System::String ^ Symbol {
    System::String ^ get() {
      lldb::SBStream desc;
      frame_->GetSymbol().GetDescription(desc);
      return gcnew System::String(desc.GetData());
    }
  }

};

}  // namespace DebugEngine
}  // namespace YetiVSI

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

#include "lldb/API/SBValue.h"

#include "ManagedUniquePtr.h"

namespace YetiVSI {
namespace DebugEngine {

using namespace LldbApi;

// Stores LLDB's value object and acts as an interface for its API calls.
//
// SBValue is used as a tree node, with related values grouped as children of
// SBValues which represent, for example, register categories or class members.
private
ref class LLDBValue sealed : SbValue {
 public:
  LLDBValue(lldb::SBValue);
  LLDBValue(lldb::SBValue, lldb::SBError);
  virtual ~LLDBValue(){};
  virtual System::String ^ GetName();
  virtual System::String ^ GetValue();
  virtual LldbApi::ValueFormat GetFormat();
  virtual void SetFormat(LldbApi::ValueFormat format);
  virtual SbType ^ GetTypeInfo();
  virtual System::String ^ GetTypeName();
  virtual System::String ^ GetSummary();
  virtual ValueType GetValueType();
  virtual SbError ^ GetError();
  virtual uint32_t GetNumChildren();
  virtual SbValue ^ GetChildAtIndex(uint32_t index);
  virtual System::Collections::Generic::List<SbValue ^> ^ GetChildren(
      uint32_t indexOffset, uint32_t count);
  virtual SbValue ^ CreateValueFromExpression(System::String ^ name,
                                              System::String ^ expression,
                                              SbExpressionOptions ^ options);
  virtual SbValue ^ CreateValueFromAddress(System::String ^ name,
                                           uint64_t address,
                                           SbType ^ type);
  virtual SbValue ^ EvaluateExpression(System::String ^ expression,
                                       SbExpressionOptions ^ options);
  virtual SbValue ^ Clone();
  virtual SbValue ^ Dereference();
  virtual SbValue ^ GetChildMemberWithName(System::String ^ name);
  virtual SbValue ^ AddressOf();
  virtual bool TypeIsPointerType();
  virtual SbValue ^ GetValueForExpressionPath(System::String ^ expressionPath);
  virtual bool GetExpressionPath(
      [System::Runtime::InteropServices::Out] System::String ^ % path);
  virtual uint64_t GetByteSize();
  virtual uint64_t GetValueAsUnsigned();

  virtual array<unsigned char> ^ GetPointeeAsByteString(uint32_t charSize, uint32_t maxStringSize,
      [System::Runtime::InteropServices::Out] System::String ^ % error);

  lldb::SBValue GetNativeObject();

 private:
  virtual array<unsigned char> ^ GetLocalArrayDataAsString(
      uint32_t charSize, uint32_t maxStringSize,
      [System::Runtime::InteropServices::Out] System::String ^ % error) sealed;

 private:
  ManagedUniquePtr<lldb::SBValue> ^ value_;
  ManagedUniquePtr<lldb::SBError> ^ error_;
};

}  // namespace DebugEngine
}  // namespace YetiVSI

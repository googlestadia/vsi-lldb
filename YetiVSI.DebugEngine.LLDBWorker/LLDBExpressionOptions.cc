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

#include "LLDBExpressionOptions.h"

namespace YetiVSI {
namespace DebugEngine {

LLDBExpressionOptions::LLDBExpressionOptions() {
  expression_options_ = MakeUniquePtr<lldb::SBExpressionOptions>();
}

void LLDBExpressionOptions::SetAutoApplyFixIts(bool b) {
  expression_options_->SetAutoApplyFixIts(b);
}

void LLDBExpressionOptions::SetIgnoreBreakpoints(bool b) {
  expression_options_->SetIgnoreBreakpoints(b);
}

lldb::SBExpressionOptions LLDBExpressionOptions::GetNativeObject() {
  return *(*expression_options_).Get();
}

}  // namespace DebugEngine
}  // namespace YetiVSI

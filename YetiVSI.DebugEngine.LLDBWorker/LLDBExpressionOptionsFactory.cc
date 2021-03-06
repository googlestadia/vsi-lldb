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

#include "LLDBExpressionOptionsFactory.h"

#include <msclr\marshal_cppstd.h>

#include "lldb/API/SBExpressionOptions.h"

#include "LLDBExpressionOptions.h"

namespace YetiVSI {
namespace DebugEngine {

SbExpressionOptions ^ LLDBExpressionOptionsFactory::Create() {
  return gcnew LLDBExpressionOptions();
}

SbExpressionOptions ^ LLDBExpressionOptionsFactory::CreateDefault() {
  SbExpressionOptions ^ result = Create();
  result->SetAutoApplyFixIts(false);
  result->SetIgnoreBreakpoints(true);
  return result;
}

}  // namespace DebugEngine
}  // namespace YetiVSI

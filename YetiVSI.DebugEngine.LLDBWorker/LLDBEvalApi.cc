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

#include "LLDBEvalApi.h"

#include <msclr/marshal_cppstd.h>

#include "LLDBError.h"
#include "LLDBStackFrame.h"
#include "LLDBValue.h"
#include "lldb-eval/api.h"
#include "lldb/API/SBFrame.h"
#include "lldb/API/SBValue.h"

namespace YetiVSI {
namespace DebugEngine {

using System::Collections::Generic::IDictionary;

SbValue ^
    LldbEval::EvaluateExpression(SbFrame ^ frame, System::String ^ expression) {
  std::string expr = msclr::interop::marshal_as<std::string>(expression);
  lldb::SBFrame sbFrame = safe_cast<LLDBStackFrame ^>(frame)->GetNativeObject();

  lldb::SBError error;
  lldb::SBValue value =
    lldb_eval::EvaluateExpression(sbFrame, expr.c_str(), error);

  return gcnew LLDBValue(value, error);
}

SbValue ^
    LldbEval::EvaluateExpression(
        SbValue ^ value, System::String ^ expression,
        IDictionary<System::String ^, SbValue ^> ^ contextVars) {
  std::string expr = msclr::interop::marshal_as<std::string>(expression);
  lldb::SBValue sbValue = safe_cast<LLDBValue^>(value)->GetNativeObject();

  // Convert `IDictionary` to `std::vector`.
  msclr::interop::marshal_context context;
  std::vector<lldb_eval::ContextVariable> vars;
  for each (auto var in contextVars) {
    const char* name = context.marshal_as<const char*>(var.Key);
    lldb::SBValue value = safe_cast<LLDBValue ^>(var.Value)->GetNativeObject();
    vars.push_back({name, value});
  }

  lldb_eval::ContextVariableList var_list{vars.data(), vars.size()};

  lldb::SBError error;
  lldb::SBValue result =
    lldb_eval::EvaluateExpression(sbValue, expr.c_str(), var_list, error);

  return gcnew LLDBValue(result, error);
}

}  // namespace DebugEngine
}  // namespace YetiVSI

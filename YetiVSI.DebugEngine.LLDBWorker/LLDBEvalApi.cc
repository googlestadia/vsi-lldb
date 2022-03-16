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
#include "LLDBTarget.h"
#include "LLDBType.h"
#include "LLDBValue.h"
#include "ValueUtil.h"
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

  // "Frame" expression evaluations are coming from the Immediate Window and the
  // Watch Window. These expressions are typically interactive (i.e. typed by
  // the user directly) and therefore side effects to the target process (e.g.
  // modifying the value of a variable) are expected to work.
  lldb_eval::Options opts;
  opts.allow_side_effects = true;

  lldb::SBError error;
  lldb::SBValue value =
      lldb_eval::EvaluateExpression(sbFrame, expr.c_str(), opts, error);

  // Try converting the result to dynamic type. That way the VSI extension will
  // be able to pick up the correct Natvis visualization.
  value = ConvertToDynamicValue(value);

  return gcnew LLDBValue(value, error);
}

SbValue ^
    LldbEval::EvaluateExpression(SbValue ^ value, System::String ^ expression,
                                 IDictionary<System::String ^, SbValue ^> ^
                                     contextVars) {
  std::string expr = msclr::interop::marshal_as<std::string>(expression);
  lldb::SBValue sbValue = safe_cast<LLDBValue ^>(value)->GetNativeObject();

  // Convert `IDictionary` to `std::vector`.
  msclr::interop::marshal_context context;
  std::vector<lldb_eval::ContextVariable> vars;
  for each (auto var in contextVars) {
    const char* name = context.marshal_as<const char*>(var.Key);
    lldb::SBValue value = safe_cast<LLDBValue ^>(var.Value)->GetNativeObject();
    vars.push_back({name, value});
  }

  // "Value" expression evaluations are coming from NatVis engine. These
  // expressions are defined in NatVis scripts and supposed to be idempotent.
  // Thus side effects to the target process are not allowed.
  lldb_eval::Options opts;
  opts.allow_side_effects = false;
  opts.context_vars = {vars.data(), vars.size()};

  lldb::SBError error;
  lldb::SBValue result =
      lldb_eval::EvaluateExpression(sbValue, expr.c_str(), opts, error);

  // Try converting the result to dynamic type. That way the VSI extension will
  // be able to pick up the correct Natvis visualization.
  result = ConvertToDynamicValue(result);

  return gcnew LLDBValue(result, error);
}

System::Tuple<SbType ^, SbError ^> ^ LldbEval::CompileExpression(
    SbTarget ^ target, SbType ^ scope, System::String ^ expression,
    IDictionary<System::String ^, SbType ^> ^ contextArgs) {
  std::string expr = msclr::interop::marshal_as<std::string>(expression);
  lldb::SBTarget sbTarget = safe_cast<LLDBTarget ^>(target)->GetNativeObject();
  lldb::SBType sbType = safe_cast<LLDBType ^>(scope)->GetNativeObject();

  // Convert `IDictionary` to `std::vector`.
  msclr::interop::marshal_context context;
  std::vector<lldb_eval::ContextArgument> args;
  for each (auto arg in contextArgs) {
    const char* name = context.marshal_as<const char*>(arg.Key);
    lldb::SBType type = safe_cast<LLDBType ^>(arg.Value)->GetNativeObject();
    args.push_back({name, type});
  }

  // Calls to this method are coming from NatVis engine and supposed to be
  // idempotent. Thus side effects to the target process are not allowed.
  lldb_eval::Options opts;
  opts.allow_side_effects = false;
  opts.context_args = {args.data(), args.size()};

  lldb::SBError error;
  std::shared_ptr<lldb_eval::CompiledExpr> compiledExpr =
      lldb_eval::CompileExpression(sbTarget, sbType, expr.c_str(), opts, error);

  // TODO: In the future we should return the entire compiled
  // expression, but right now the information about resulting type is enough.
  lldb::SBType resultType =
      compiledExpr != nullptr ? compiledExpr->result_type : lldb::SBType();
  SbType ^ lldbType = gcnew LLDBType(resultType);
  SbError ^ lldbError = gcnew LLDBError(error);
  return gcnew System::Tuple<SbType ^, SbError ^>(lldbType, lldbError);
}

}  // namespace DebugEngine
}  // namespace YetiVSI

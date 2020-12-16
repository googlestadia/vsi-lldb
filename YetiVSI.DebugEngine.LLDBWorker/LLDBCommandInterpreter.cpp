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

#include "LLDBCommandInterpreter.h"

#include <msclr\marshal_cppstd.h>

#include "LLDBCommandReturnObject.h"
#include "ReturnStatusUtil.h"

#include "lldb/API/SBCommandInterpreter.h"
#include "lldb/API/SBCommandReturnObject.h"


namespace YetiVSI {
namespace DebugEngine {

LLDBCommandInterpreter::LLDBCommandInterpreter(lldb::SBCommandInterpreter interpreter) {
  interpreter_ = MakeUniquePtr<lldb::SBCommandInterpreter>(interpreter);
}

ReturnStatus LLDBCommandInterpreter::HandleCommand(System::String ^ command,
  LldbApi::SbCommandReturnObject ^% result) {
  lldb::SBCommandReturnObject lldb_result;
  lldb::ReturnStatus lldb_return_status = interpreter_->HandleCommand(
    msclr::interop::marshal_as<std::string>(command).c_str(),
    lldb_result);
  if (lldb_result.IsValid()) {
    result = gcnew LLDBCommandReturnObject(lldb_result);
    return ConvertReturnStatus(lldb_return_status);
  }
  result = nullptr;
  return ReturnStatus::Invalid;
}

void LLDBCommandInterpreter::SourceInitFileInHomeDirectory() {
  lldb::SBCommandReturnObject lldb_result;
  interpreter_->SourceInitFileInHomeDirectory(lldb_result);
}

}  // namespace DebugEngine
}  // namespace YetiVSI

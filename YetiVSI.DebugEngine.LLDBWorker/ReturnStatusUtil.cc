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

#include "ValueTypeUtil.h"

namespace YetiVSI {
namespace DebugEngine {

LldbApi::ReturnStatus ConvertReturnStatus(lldb::ReturnStatus lldb_return_status) {
  switch (lldb_return_status) {
    case lldb::eReturnStatusSuccessFinishNoResult:
      return LldbApi::ReturnStatus::SuccessFinishNoResult;
    case lldb::eReturnStatusSuccessFinishResult:
      return LldbApi::ReturnStatus::SuccessFinishResult;
    case lldb::eReturnStatusSuccessContinuingNoResult:
      return LldbApi::ReturnStatus::SuccessContinuingNoResult;
    case lldb::eReturnStatusSuccessContinuingResult:
      return LldbApi::ReturnStatus::SuccessContinuingResult;
    case lldb::eReturnStatusStarted:
      return LldbApi::ReturnStatus::Started;
    case lldb::eReturnStatusFailed:
      return LldbApi::ReturnStatus::Failed;
    case lldb::eReturnStatusQuit:
      return LldbApi::ReturnStatus::Quit;
    case lldb::eReturnStatusInvalid:
    // Fall-through
    default:
      return LldbApi::ReturnStatus::Invalid;
  }
}

}  // namespace DebugEngine
}  // namespace YetiVSI

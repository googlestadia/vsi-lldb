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

#include "LLDBPlatform.h"

#include "lldb/API/SBError.h"

#include "LLDBError.h"
#include "LLDBPlatformConnectOptions.h"
#include "LLDBPlatformShellCommand.h"

namespace YetiVSI {
namespace DebugEngine {

LLDBPlatform::LLDBPlatform(lldb::SBPlatform platform) {
  platform_ = MakeUniquePtr<lldb::SBPlatform>(platform);
}

SbError ^ LLDBPlatform::ConnectRemote(SbPlatformConnectOptions ^
                                             connect_options) {
  LLDBPlatformConnectOptions ^ lldbPlatformConnectOptions =
      safe_cast<LLDBPlatformConnectOptions ^>(connect_options);
  lldb::SBPlatformConnectOptions sbPlatformConnectOptions =
      lldbPlatformConnectOptions->GetNativeObject();
  lldb::SBError error = platform_->ConnectRemote(sbPlatformConnectOptions);
  return gcnew LLDBError(error);
}

SbError ^ LLDBPlatform::Run(SbPlatformShellCommand ^ command) {
  LLDBPlatformShellCommand ^ lldbPlatformShellCommand =
      safe_cast<LLDBPlatformShellCommand ^>(command);
  // When copying platform shell commands, a deep copy is made.  To ensure we
  // update the state for the LLDBPlatformShellCommand object, we want a pointer
  // to the underlying SBPlatformShellCommand.
  lldb::SBPlatformShellCommand* sbPlatformShellCommand =
      lldbPlatformShellCommand->GetNativeObjectPtr();
  lldb::SBError error = platform_->Run(*sbPlatformShellCommand);
  return gcnew LLDBError(error);
}

lldb::SBPlatform LLDBPlatform::GetNativeObject() { return *(*platform_).Get(); }
}  // namespace DebugEngine
}  // namespace YetiVSI
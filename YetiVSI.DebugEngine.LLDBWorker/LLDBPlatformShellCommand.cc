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

#include <msclr/marshal_cppstd.h>

#include "lldb/API/SBError.h"

#include "LLDBError.h"
#include "LLDBPlatformShellCommand.h"

namespace YetiVSI {
namespace DebugEngine {

LLDBPlatformShellCommand::LLDBPlatformShellCommand(
    lldb::SBPlatformShellCommand command) {
  platform_shell_command_ =
      MakeUniquePtr<lldb::SBPlatformShellCommand>(command);
}

lldb::SBPlatformShellCommand* LLDBPlatformShellCommand::GetNativeObjectPtr() {
  return (*platform_shell_command_).Get();
}

System::String ^ LLDBPlatformShellCommand::GetOutput() {
  return gcnew System::String(platform_shell_command_->GetOutput());
}

int LLDBPlatformShellCommand::GetSignal() {
  return platform_shell_command_->GetSignal();
}

int LLDBPlatformShellCommand::GetStatus() {
  return platform_shell_command_->GetStatus();
}

System::String ^ LLDBPlatformShellCommand::GetCommand() {
  return gcnew System::String(platform_shell_command_->GetCommand());
}

void LLDBPlatformShellCommand::SetOutput(System::String ^ output) {
  throw gcnew System::NotImplementedException();
}

void LLDBPlatformShellCommand::SetStatus(int status) {
  throw gcnew System::NotImplementedException();
}

void LLDBPlatformShellCommand::SetSignal(int signal) {
  throw gcnew System::NotImplementedException();
}

}  // namespace DebugEngine
}  // namespace YetiVSI
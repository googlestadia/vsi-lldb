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

#include "LLDBPlatformShellCommandFactory.h"

#include <msclr/marshal_cppstd.h>

#include "LLDBPlatformShellCommand.h"

namespace YetiVSI {
namespace DebugEngine {

SbPlatformShellCommand ^
    LLDBPlatformShellCommandFactory::Create(System::String ^ command) {
  auto command_string = msclr::interop::marshal_as<std::string>(command);
  lldb::SBPlatformShellCommand shell_command(command_string.c_str());
  // We can't check IsValid on the shell command object, because it doesn't
  // support that method.
  return gcnew LLDBPlatformShellCommand(shell_command);
}
}  // namespace DebugEngine
}  // namespace YetiVSI

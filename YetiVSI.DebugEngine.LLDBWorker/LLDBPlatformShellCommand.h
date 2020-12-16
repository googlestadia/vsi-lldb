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

#include "lldb/API/SBPlatform.h"

#include "ManagedUniquePtr.h"

namespace YetiVSI {
namespace DebugEngine {

using namespace LldbApi;

private
ref class LLDBPlatformShellCommand sealed : SbPlatformShellCommand {
 public:
  LLDBPlatformShellCommand(lldb::SBPlatformShellCommand);
  virtual ~LLDBPlatformShellCommand(){};
  virtual System::String ^ GetOutput();
  virtual int GetSignal();
  virtual int GetStatus();
  virtual System::String ^ GetCommand();
  virtual void SetOutput(System::String ^ output);
  virtual void SetSignal(int signal);
  virtual void SetStatus(int status);

  // Get the underlying lldb object.
  lldb::SBPlatformShellCommand* GetNativeObjectPtr();

 private:
  ManagedUniquePtr<lldb::SBPlatformShellCommand> ^ platform_shell_command_;
};

}  // namespace DebugEngine
}  // namespace YetiVSI

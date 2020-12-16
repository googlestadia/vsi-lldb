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

#include "LLDBPlatformConnectOptions.h"

#include "lldb/API/SBPlatform.h"

namespace YetiVSI {
namespace DebugEngine {

LLDBPlatformConnectOptions::LLDBPlatformConnectOptions(
    lldb::SBPlatformConnectOptions connect_options) {
  platform_connect_options_ =
      MakeUniquePtr<lldb::SBPlatformConnectOptions>(connect_options);
}

lldb::SBPlatformConnectOptions LLDBPlatformConnectOptions::GetNativeObject() {
  return *(*platform_connect_options_).Get();
}

System::String ^ LLDBPlatformConnectOptions::GetUrl() {
  return gcnew System::String(platform_connect_options_->GetURL());
}

}  // namespace DebugEngine
}  // namespace YetiVSI
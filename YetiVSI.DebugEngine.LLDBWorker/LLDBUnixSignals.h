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

#include "lldb/API/SBUnixSignals.h"

#include "ManagedUniquePtr.h"

namespace YetiVSI {
namespace DebugEngine {

using namespace LldbApi;

private ref class LLDBUnixSignals sealed : SbUnixSignals {
 public:
  LLDBUnixSignals(lldb::SBUnixSignals);
  virtual ~LLDBUnixSignals() {}

  virtual bool GetShouldStop(int32_t signalNumber);
  virtual bool SetShouldStop(int32_t signalNumber, bool value);

 private:
  ManagedUniquePtr<lldb::SBUnixSignals> ^ signals_;
};

}  // namespace DebugEngine
}  // namespace YetiVSI

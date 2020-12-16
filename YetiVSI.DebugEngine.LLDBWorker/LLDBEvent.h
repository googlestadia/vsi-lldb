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

#include "lldb/API/SBEvent.h"

#include "ManagedUniquePtr.h"

namespace YetiVSI {
namespace DebugEngine {

using namespace LldbApi;

// Interface for a debugger event.
private
ref class LLDBEvent sealed : SbEvent {
 public:
  LLDBEvent(lldb::SBEvent);
  virtual ~LLDBEvent(){};

  virtual EventType GetEventType();
  virtual System::String ^ GetDescription();
  virtual StateType GetStateType();
  virtual bool GetProcessRestarted();

 private:
  ManagedUniquePtr<lldb::SBEvent> ^ event_;
};

}  // namespace DebugEngine
}  // namespace YetiVSI

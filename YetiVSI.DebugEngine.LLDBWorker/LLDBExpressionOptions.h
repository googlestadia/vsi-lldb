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

#include "lldb/API/SBExpressionOptions.h"

#include "ManagedUniquePtr.h"

namespace YetiVSI {
namespace DebugEngine {

using namespace LldbApi;

private
ref class LLDBExpressionOptions sealed : SbExpressionOptions {
 public:
  LLDBExpressionOptions();
  virtual ~LLDBExpressionOptions(){};
  virtual void SetAutoApplyFixIts(bool b);
  virtual void SetIgnoreBreakpoints(bool b);

  lldb::SBExpressionOptions GetNativeObject();

 private:
  ManagedUniquePtr<lldb::SBExpressionOptions> ^ expression_options_;
};

}  // namespace DebugEngine
}  // namespace YetiVSI

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

#include "ManagedUniquePtr.h"

namespace YetiVSI {
namespace DebugEngine {

using namespace LldbApi;
using System::Collections::Generic::IDictionary;

public
ref class LldbEval abstract sealed {
 public:
  static SbValue ^
      EvaluateExpression(SbFrame ^ frame, System::String ^ expression);

  static SbValue ^
      EvaluateExpression(SbValue ^ value, System::String ^ expression,
                         IDictionary<System::String ^, SbValue ^> ^ contextVars);

  static System::Tuple<SbType ^, SbError ^> ^
      CompileExpression(SbTarget ^ target, SbType ^ scope, System::String ^ expression,
                        IDictionary<System::String ^, SbType ^> ^ contextArgs);
};

}  // namespace DebugEngine
}  // namespace YetiVSI
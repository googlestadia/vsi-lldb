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

using System;
using System.Collections.Generic;

namespace LldbApi
{
    // Interface mirrors the SBFrame API as closely as possible.
    public interface SbFrame
    {
        // Returns the name of the current function, if it can be determined.
        string GetFunctionName();

        // Returns the function object that this frame represents.
        SbFunction GetFunction();

        // Returns the symbol object that this frame represents.
        SbSymbol GetSymbol();

        // Get info about the frame's variables, including parameters and statics.
        List<SbValue> GetVariables(
            bool arguments, bool locals, bool statics, bool only_in_scope);

        // Get info about the frame's registers.
        List<SbValue> GetRegisters();

        // Find a value for a variable expression path like "rect.origin.x" or
        // "pt_ptr->x", "*self", "*this->obj_ptr". The returned value is _not_
        // an expression result and is not a constant object like
        // SbFrame::EvaluateExpression(...) returns, but a child object of
        // the variable value.
        SbValue GetValueForVariablePath(string varPath);

        // Find variables, register sets, registers, or persistent variables using
        // the frame as the scope.
        //
        // NB. This function does not look up ivars in the function object pointer.
        // To do that use GetValueForVariablePath.
        //
        // The version that doesn't supply a 'use_dynamic' value will use the
        // target's default.
        SbValue FindValue(string varName, ValueType value_type);

        // Get the frame's module.
        SbModule GetModule();

        // Get the line entry for this frame, or null if none is available.
        SbLineEntry GetLineEntry();

        // Get the thread associated with this frame.
        SbThread GetThread();

        // Returns the address in the program counter.
        ulong GetPC();

        // Evaluates the supplied expression and returns the result.
        SbValue EvaluateExpression(string text, SbExpressionOptions options);
    }
}

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

using DebuggerCommonApi;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DebuggerApi
{
     /// <summary>
    /// Interface based off of the SBFrame API.
    /// </summary>
    public interface RemoteFrame
    {
        /// <summary>
        /// Returns the name of the current function, if it can be determined, and pre-processes it
        /// (strips args, leading ::).
        ///</summary>
        string GetFunctionName();

        /// <summary>
        /// Returns the name of the current function, if it can be determined. Does not strip
        /// arguments, or leading ::.
        ///</summary>
        string GetFunctionNameWithSignature();

        /// <summary>
        /// Returns the function object that this frame represents.
        ///</summary>
        SbFunction GetFunction();

        /// <summary>
        /// Returns the symbol object that this frame represents.
        ///</summary>
        SbSymbol GetSymbol();

        /// <summary>
        /// Get info about the frame's variables, including parameters and statics.
        ///</summary>
        List<RemoteValue> GetVariables(
            bool arguments, bool locals, bool statics, bool only_in_scope);

        /// <summary>
        /// Get info about the frame's registers.
        ///</summary>
        List<RemoteValue> GetRegisters();

        /// <summary>
        /// Find a value for a variable expression path like "rect.origin.x" or
        /// "pt_ptr->x", "*self", "*this->obj_ptr". The returned value is _not_
        /// an expression result and is not a constant object like
        /// RemoteFrame::EvaluateExpression(...) returns, but a child object of
        /// the variable value.
        ///</summary>
        RemoteValue GetValueForVariablePath(string varPath);

        /// <summary>
        /// <para>Find variables, register sets, registers, or persistent variables
        /// using the frame as the scope.</para>
        ///
        /// <para>NB. This function does not look up ivars in the function object
        /// pointer. To do that use GetValueForVariablePath.</para>
        ///
        /// <para>The version that doesn't supply a 'use_dynamic' value will use
        /// the target's default.</para>
        ///</summary>
        RemoteValue FindValue(string varName, ValueType value_type);

        /// <summary>
        /// Get the frame's module.
        ///</summary>
        SbModule GetModule();

        /// <summary>
        /// Get the line entry for this frame, or null if none is available.
        ///</summary>
        LineEntryInfo GetLineEntry();

        /// <summary>
        /// Get the thread associated with this frame.
        ///</summary>
        RemoteThread GetThread();

        /// <summary>
        /// Returns the address in the program counter.
        /// </summary>
        ulong GetPC();

        /// <summary>
        /// Sets the program counter value.
        /// </summary>
        bool SetPC(ulong addr);

        /// <summary>
        /// Evaluates the supplied expression asynchronously and returns the result.
        /// </summary>
        Task<RemoteValue> EvaluateExpressionAsync(string text);

        /// <summary>
        /// Evaluates the expression using lldb_eval.
        /// </summary>
        Task<RemoteValue> EvaluateExpressionLldbEvalAsync(string text);

        /// <summary>
        /// Get the stack frames address range.
        /// </summary>
        AddressRange GetPhysicalStackRange();

        /// <summary>
        /// Retrieves requested information about this stack frame.
        /// </summary>
        /// <param name="fields">Specifies what should be retrieved.</param>
        FrameInfo<SbModule>? GetInfo(FrameInfoFlags fields);
    }
}

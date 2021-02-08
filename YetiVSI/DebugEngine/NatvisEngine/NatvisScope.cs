// Copyright 2021 Google LLC
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

using DebuggerApi;
using System.Collections.Generic;

namespace YetiVSI.DebugEngine.NatvisEngine
{
    /// <summary>
    /// A class containing Natvis-specific scope data (e.g. context variables, tokens that should
    /// be replaced in expressions).
    /// </summary>
    public class NatvisScope
    {
        /// <summary>
        /// Natvis expression replacement tokens.
        /// Examples: ("$i" => "2U"), ("T1" => "int"), ("var" => "$var_1")
        /// </summary>
        public IDictionary<string, string> ScopedNames { get; private set; }

        /// <summary>
        /// Context variables that can be used by lldb-eval. Natvis uses context variables to
        /// create and manipulate scope values created by Natvis' <Variable> tag.
        /// </summary>
        public IDictionary<string, RemoteValue> ContextVariables { get; private set; }

        public NatvisScope()
        {
            ScopedNames = new Dictionary<string, string>();
            ContextVariables = new Dictionary<string, RemoteValue>();
        }

        /// <summary>
        /// Constructs a copy of the other natvis scope.
        /// </summary>
        public NatvisScope(NatvisScope other)
        {
            ScopedNames = new Dictionary<string, string>(other.ScopedNames);
            ContextVariables = new Dictionary<string, RemoteValue>(other.ContextVariables);
        }

        public void SetScopedName(string name, string transformedName)
        {
            ScopedNames[name] = transformedName;
        }

        public void AddContextVariable(string name, RemoteValue value)
        {
            ContextVariables[name] = value;
        }
    }
}

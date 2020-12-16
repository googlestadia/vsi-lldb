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
using static Microsoft.VisualStudio.Shell.RegistrationAttribute;
using System.Collections.Generic;
using TestsCommon.TestSupport;

namespace YetiVSI.Test.TestSupport
{
    public class KeyStub : Key
    {
        readonly string name;
        public readonly Dictionary<string, object> values = new Dictionary<string, object>();
        public readonly Dictionary<string, KeyStub> subKeys = new Dictionary<string, KeyStub>();

        public KeyStub(string name)
        {
            this.name = name;
        }

        public override Key CreateSubkey(string name)
        {
            var key = new KeyStub(name);
            // I'm not familiar with the behaviour of adding two sub keys with the same name,
            // so just fail in that case.
            subKeys.Add(name, key);
            return key;
        }

        public override void SetValue(string valueName, object value)
        {
            values[valueName] = value;
        }

        #region Not Implemented
        public override void Close()
        {
            throw new NotImplementedTestDoubleException();
        }
        #endregion
    }
}

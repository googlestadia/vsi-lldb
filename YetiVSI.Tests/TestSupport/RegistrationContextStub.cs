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
using System.IO;
using Microsoft.VisualStudio.Shell;
using static Microsoft.VisualStudio.Shell.RegistrationAttribute;
using System.Collections.Generic;
using TestsCommon.TestSupport;

namespace YetiVSI.Test.TestSupport
{
    public class RegistrationContextStub : RegistrationContext
    {
        public Dictionary<string, KeyStub> keys = new Dictionary<string, KeyStub>();

        public override Key CreateKey(string name)
        {
            var key = new KeyStub(name);
            keys.Add(name, key);
            return key;
        }

        public override void RemoveKey(string name)
        {
            keys.Remove(name);
        }

        #region Not Implemented
        public override string CodeBase
        {
            get
            {
                throw new NotImplementedTestDoubleException();
            }
        }

        public override string ComponentPath
        {
            get
            {
                throw new NotImplementedTestDoubleException();
            }
        }

        public override Type ComponentType
        {
            get
            {
                throw new NotImplementedTestDoubleException();
            }
        }

        public override string InprocServerPath
        {
            get
            {
                throw new NotImplementedTestDoubleException();
            }
        }

        public override TextWriter Log
        {
            get
            {
                throw new NotImplementedTestDoubleException();
            }
        }

        public override RegistrationMethod RegistrationMethod
        {
            get
            {
                throw new NotImplementedTestDoubleException();
            }
        }

        public override string RootFolder
        {
            get
            {
                throw new NotImplementedTestDoubleException();
            }
        }

        public override string EscapePath(string str)
        {
            throw new NotImplementedTestDoubleException();
        }

        public override void RemoveKeyIfEmpty(string name)
        {
            throw new NotImplementedTestDoubleException();
        }

        public override void RemoveValue(string keyname, string valuename)
        {
            throw new NotImplementedTestDoubleException();
        }
        #endregion
    }
}

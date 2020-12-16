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

using Debugger.Common;
using NUnit.Framework;
using System;
using YetiCommon;

namespace YetiVSI.Test.DebuggerGrpc
{
    [TestFixture]
    class CommonTests
    {
        [Test]
        public void DebuggerApiToCommonValueFormatConversions()
        {
            foreach (ValueFormat format in Enum.GetValues(typeof(DebuggerApi.ValueFormat)))
            {
                format.ConvertTo<Debugger.Common.ValueFormat>();
            }
        }

        [Test]
        public void CommonToDebuggerApiValueFormatConversions()
        {
            foreach (ValueFormat format in Enum.GetValues(typeof(Debugger.Common.ValueFormat)))
            {
                format.ConvertTo<DebuggerApi.ValueFormat>();
            }
        }

        [Test]
        public void DebuggerApiToCommonValueTypeConversions()
        {
            foreach (DebuggerApi.ValueType type in Enum.GetValues(typeof(DebuggerApi.ValueType)))
            {
                type.ConvertTo<Debugger.Common.ValueType>();
            }
        }

        [Test]
        public void CommonToDebuggerApiValueTypeConversions()
        {
            foreach (Debugger.Common.ValueType type in Enum.GetValues(typeof(Debugger.Common.ValueType)))
            {
                type.ConvertTo<DebuggerApi.ValueType>();
            }
        }

        [Test]
        public void LldbApiToCommonValueFormatConversions()
        {
            foreach (ValueFormat format in Enum.GetValues(typeof(LldbApi.ValueFormat)))
            {
                format.ConvertTo<Debugger.Common.ValueFormat>();
            }
        }

        [Test]
        public void CommonToLldbApiValueFormatConversions()
        {
            foreach (ValueFormat format in Enum.GetValues(typeof(Debugger.Common.ValueFormat)))
            {
                format.ConvertTo<LldbApi.ValueFormat>();
            }
        }

        [Test]
        public void LldbApiToCommonValueTypeConversions()
        {
            foreach (LldbApi.ValueType type in Enum.GetValues(typeof(LldbApi.ValueType)))
            {
                type.ConvertTo<Debugger.Common.ValueType>();
            }
        }

        [Test]
        public void CommonToLldbApiValueTypeConversions()
        {
            foreach (Debugger.Common.ValueType type in Enum.GetValues(typeof(Debugger.Common.ValueType)))
            {
                type.ConvertTo<LldbApi.ValueType>();
            }
        }
    }
}

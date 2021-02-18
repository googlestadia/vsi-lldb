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

using System.Threading.Tasks;
using DebuggerApi;
using Microsoft.VisualStudio.Threading;
using NUnit.Framework;
using TestsCommon.TestSupport;
using YetiVSI.DebugEngine.Variables;
using YetiVSI.Test.MediumTestsSupport;
using YetiVSI.Test.TestSupport;

namespace YetiVSI.Test.DebugEngine.Variables
{
    class ExpandVariableInformationDecoratorTests
    {
        MediumTestDebugEngineFactoryCompRoot _compRoot;
        LogSpy _logSpy;
        const string _memAddress1 = "0x0000000002260771";

        [SetUp]
        public void SetUp()
        {
            _compRoot = new MediumTestDebugEngineFactoryCompRoot(new JoinableTaskContext());
            _logSpy = new LogSpy();
            _logSpy.Attach();
        }

        [TearDown]
        public void TearDown()
        {
            _logSpy.Detach();
        }

        [Test]
        public async Task ExpandFormatSpecifierAsync()
        {
            int[] childValues = {0, 1, 2, 3, 4};
            var remoteValue = RemoteValueFakeUtil.CreateSimpleIntArray("arr", childValues);

            var varInfo = CreateVarInfo(remoteValue, "expand(3)");

            Assert.That(_logSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(_logSpy.GetOutput(), Does.Not.Contain("WARNING"));

            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("3"));
        }

        [Test]
        public async Task ExpandFormatSpecifierReturnsChildAsync()
        {
            int[] grandChildValues = {0, 1};
            var child = RemoteValueFakeUtil.CreateSimpleIntArray("arr", grandChildValues);
            var remoteValue = RemoteValueFakeUtil.CreatePointer("int**", "ptr", _memAddress1);
            remoteValue.AddChild(child);

            var varInfo = CreateVarInfo(remoteValue, "expand(0)");
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(_logSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(_logSpy.GetOutput(), Does.Not.Contain("WARNING"));

            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("0"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("1"));
        }

        [Test]
        public async Task ExpandFormatSpecifierOutOfRangeAsync()
        {
            int[] childValues = {0, 1, 2, 3, 4};
            var remoteValue = RemoteValueFakeUtil.CreateSimpleIntArray("arr", childValues);

            var varInfo = CreateVarInfo(remoteValue, "expand(6)");
            Assert.That(varInfo.Error, Is.EqualTo(true));
            Assert.That(await varInfo.ValueAsync(),
                        Is.EqualTo("<out-of-bounds child index in 'expand()' format specifier>"));
        }

        IVariableInformation CreateVarInfo(RemoteValue remoteValue, string formatSpecifier) =>
            _compRoot.GetVariableInformationFactory().Create(remoteValue, remoteValue.GetName(),
                                                             new FormatSpecifier(formatSpecifier));
    }
}
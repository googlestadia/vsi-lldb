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

﻿using DebuggerApi;
using NSubstitute;
using NUnit.Framework;
using System.Threading.Tasks;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.Test.DebugEngine.Variables
{
    [TestFixture]
    class CachingVariableInformationTests
    {
        CachingVariableInformation _cachingVariable;
        IVariableInformation _wrappedVariable;

        [SetUp]
        public void SetUp()
        {
            _wrappedVariable = Substitute.For<IVariableInformation>();
            _cachingVariable = new CachingVariableInformation(_wrappedVariable);
        }

        [Test]
        public async Task DelegatesCallsToWrappedVariableWhenAccessedFirstTimeAsync()
        {
            await _cachingVariable.GetChildAdapterAsync();
            await _wrappedVariable.Received(1).GetChildAdapterAsync();

            _cachingVariable.GetCachedView();
            _wrappedVariable.Received(1).GetCachedView();

            await _cachingVariable.StringViewAsync();
            await _wrappedVariable.Received(1).StringViewAsync();
        }

        [Test]
        public async Task UseCachedValuesIfAlreadyInitializedAsync()
        {
            await _cachingVariable.GetChildAdapterAsync();
            await _cachingVariable.GetChildAdapterAsync();

            await _wrappedVariable.Received(1).GetChildAdapterAsync();

            _cachingVariable.GetCachedView();
            _cachingVariable.GetCachedView();

            _wrappedVariable.Received(1).GetCachedView();

            await _cachingVariable.StringViewAsync();
            await _cachingVariable.StringViewAsync();
            await _wrappedVariable.Received(1).StringViewAsync();
        }

        [Test]
        public async Task InvalidatesCacheWhenFallbackValueIsChangedAsync()
        {
            _wrappedVariable.FallbackValueFormat.Returns(ValueFormat.Default);

            await _cachingVariable.GetChildAdapterAsync();
            _cachingVariable.FallbackValueFormat = ValueFormat.Hex;
            await _cachingVariable.GetChildAdapterAsync();

            await _wrappedVariable.Received(2).GetChildAdapterAsync();

            _cachingVariable.GetCachedView();
            _cachingVariable.FallbackValueFormat = ValueFormat.Binary;
            _cachingVariable.GetCachedView();

            _wrappedVariable.Received(2).GetCachedView();

            await _cachingVariable.StringViewAsync();
            _cachingVariable.FallbackValueFormat = ValueFormat.Boolean;
            await _cachingVariable.StringViewAsync();
            await _wrappedVariable.Received(2).StringViewAsync();
        }
    }
}
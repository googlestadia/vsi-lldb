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

using DebuggerApi;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Threading;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YetiVSI.DebugEngine;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class ChildrenProviderTest
    {
        const enum_DEBUGPROP_INFO_FLAGS _nameFlag = enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME;

        RemoteTarget _target;
        ChildrenProvider.Factory _childrenProviderFactory;
        IChildrenProvider _childrenProvider;
        List<IVariableInformation> _children;


        [SetUp]
        public void SetUp()
        {
            FillThreeChildren();

            _target = Substitute.For<RemoteTarget>();
            _childrenProviderFactory = new ChildrenProvider.Factory();

#pragma warning disable VSSDK005 // Avoid instantiating JoinableTaskContext
            var taskExecutor = new TaskExecutor(new JoinableTaskContext().Factory);
#pragma warning restore VSSDK005 // Avoid instantiating JoinableTaskContext
            var enumFactory = new VariableInformationEnum.Factory(taskExecutor);
            var propertyFactory = new DebugAsyncProperty.Factory(
                enumFactory, _childrenProviderFactory, Substitute.For<DebugCodeContext.Factory>(),
                new VsExpressionCreator(), taskExecutor);

            _childrenProviderFactory.Initialize(propertyFactory);

            _childrenProvider = _childrenProviderFactory.Create(
                _target, new ListChildAdapter.Factory().Create(_children), _nameFlag, 0);
        }

        [Test]
        public void FactoryThrowsIfCreatePropertyDelegateIsNull()
        {
            _childrenProviderFactory.Initialize(null);
            Assert.Throws<NullReferenceException>(
                () => _childrenProviderFactory.Create(
                    _target, new ListChildAdapter.Factory().Create(_children), _nameFlag, 0));
        }

        [Test]
        public async Task GetChildrenCountReturnsNumberOfChildrenAsync()
        {
            int childrenCount = await _childrenProvider.GetChildrenCountAsync();
            Assert.That(childrenCount, Is.EqualTo(_children.Count));
        }

        [Test]
        public async Task GetFirstNChildrenReturnsNElementsAsync()
        {
            const int numToFetch = 2;
            var propertyInfo = new DEBUG_PROPERTY_INFO[numToFetch];
            int numFetched = await _childrenProvider.GetChildrenAsync(0, numToFetch, propertyInfo);

            Assert.That(numFetched, Is.EqualTo(numToFetch));

            Assert.That(propertyInfo[0].bstrName, Is.EqualTo(_children[0].DisplayName));
            Assert.That(propertyInfo[1].bstrName, Is.EqualTo(_children[1].DisplayName));
        }

        [Test]
        public async Task GetChildrenReturnsAllIfMoreThanExistsRequestedAsync()
        {
            const int numToFetch = 10;
            var propertyInfo = new DEBUG_PROPERTY_INFO[numToFetch];
            int numFetched = await _childrenProvider.GetChildrenAsync(0, numToFetch, propertyInfo);

            Assert.That(numFetched, Is.EqualTo(_children.Count));

            Assert.That(propertyInfo[0].bstrName, Is.EqualTo(_children[0].DisplayName));
            Assert.That(propertyInfo[1].bstrName, Is.EqualTo(_children[1].DisplayName));
            Assert.That(propertyInfo[2].bstrName, Is.EqualTo(_children[2].DisplayName));
        }

        void FillThreeChildren()
        {
            _children = new List<IVariableInformation>();

            var child1 = Substitute.For<IVariableInformation>();
            child1.DisplayName.Returns("child1");
            child1.GetCachedView().Returns(child1);
            _children.Add(child1);

            var child2 = Substitute.For<IVariableInformation>();
            child2.DisplayName.Returns("child2");
            child2.GetCachedView().Returns(child2);
            _children.Add(child2);

            var child3 = Substitute.For<IVariableInformation>();
            child3.DisplayName.Returns("child3");
            child3.GetCachedView().Returns(child3);
            _children.Add(child3);
        }
    }
}
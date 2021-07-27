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

﻿using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using Microsoft.VisualStudio.Threading;
using YetiVSI.DebugEngine;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class VariableInformationEnumTests
    {
        ChildrenProvider.Factory _childrenProviderFactory;
        VariableInformationEnum.Factory _enumFactory;
        IEnumDebugPropertyInfo2 _varInfoEnum;
        List<IVariableInformation> _children;

        [SetUp]
        public void SetUp()
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

            var taskExecutor = new TaskExecutor(new JoinableTaskContext().Factory);
            _enumFactory = new VariableInformationEnum.Factory(taskExecutor);

            _childrenProviderFactory = new ChildrenProvider.Factory();
            var propertyFactory = new DebugAsyncProperty.Factory(
                _enumFactory, _childrenProviderFactory, Substitute.For<DebugCodeContext.Factory>(),
                new VsExpressionCreator(), taskExecutor);

            _childrenProviderFactory.Initialize(propertyFactory);

            var childrenProvider = _childrenProviderFactory.Create(
                new ListChildAdapter.Factory().Create(_children),
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME, 0);

            _varInfoEnum = _enumFactory.Create(childrenProvider);
        }

        [Test]
        public void TestGetCountEqualToChildrenCount()
        {
            int result = _varInfoEnum.GetCount(out uint count);

            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
            Assert.That(count, Is.EqualTo(_children.Count));
        }

        [Test]
        public void TestGetFirstNChildren()
        {
            var propertyInfo = new DEBUG_PROPERTY_INFO[2];
            int result = _varInfoEnum.Next(2, propertyInfo, out uint numFetched);

            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
            Assert.That(numFetched, Is.EqualTo(2));

            Assert.That(propertyInfo[0].bstrName, Is.EqualTo(_children[0].DisplayName));
            Assert.That(propertyInfo[1].bstrName, Is.EqualTo(_children[1].DisplayName));
        }

        [Test]
        public void TestGetNextNChildren()
        {
            var propertyInfo = new DEBUG_PROPERTY_INFO[1];
            int result = _varInfoEnum.Next(1, propertyInfo, out uint numFetched);

            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
            Assert.That(numFetched, Is.EqualTo(1));

            propertyInfo = new DEBUG_PROPERTY_INFO[3];
            result = _varInfoEnum.Next(3, propertyInfo, out numFetched);

            Assert.That(result, Is.EqualTo(VSConstants.S_FALSE));
            Assert.That(numFetched, Is.EqualTo(2));

            Assert.That(propertyInfo[0].bstrName, Is.EqualTo(_children[1].DisplayName));
            Assert.That(propertyInfo[1].bstrName, Is.EqualTo(_children[2].DisplayName));
        }

        [Test]
        public void TestResetAndGetFirstChildren()
        {
            var propertyInfo = new DEBUG_PROPERTY_INFO[2];
            int result = _varInfoEnum.Next(2, propertyInfo, out uint numFetched);

            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
            Assert.That(numFetched, Is.EqualTo(2));

            _varInfoEnum.Reset();

            propertyInfo = new DEBUG_PROPERTY_INFO[2];
            result = _varInfoEnum.Next(2, propertyInfo, out numFetched);

            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
            Assert.That(numFetched, Is.EqualTo(2));

            Assert.That(propertyInfo[0].bstrName, Is.EqualTo(_children[0].DisplayName));
            Assert.That(propertyInfo[1].bstrName, Is.EqualTo(_children[1].DisplayName));
        }

        [Test]
        public void TestSkipReturnsFalseWhenSkipsTooFar()
        {
            int result = _varInfoEnum.Skip(10);

            Assert.That(result, Is.EqualTo(VSConstants.S_FALSE));

            var propertyInfo = new DEBUG_PROPERTY_INFO[1];
            result = _varInfoEnum.Next(2, propertyInfo, out uint numFetched);

            Assert.That(result, Is.EqualTo(VSConstants.S_FALSE));
            Assert.That(numFetched, Is.EqualTo(0));
        }

        [Test]
        public void TestSkipAndGetChildren()
        {
            int result = _varInfoEnum.Skip(2);
            Assert.That(result, Is.EqualTo(VSConstants.S_OK));

            var propertyInfo = new DEBUG_PROPERTY_INFO[1];
            result = _varInfoEnum.Next(1, propertyInfo, out uint numFetched);

            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
            Assert.That(numFetched, Is.EqualTo(1));

            Assert.That(propertyInfo[0].bstrName, Is.EqualTo(_children[2].DisplayName));
        }
    }
}

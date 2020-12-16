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

﻿using NUnit.Framework;
using System;
using System.Linq.Expressions;

namespace YetiCommon.Tests.CastleAspects.TestSupport
{

    [TestFixture]
    public class MethodInfoUtilTests
    {
        #region helpers
        public static void StaticMethod()
        {
        }
        #endregion

        [Test]
        public void ShouldReturnMethodInfoForFunctionInBody()
        {
            var methodInfo = MethodInfoUtil.GetMethodInfo<int>(
                x => x.ToString());
            Assert.NotNull(methodInfo);
            Assert.AreEqual("ToString", methodInfo.Name);
            Assert.AreEqual(typeof(string), methodInfo.ReturnType);
        }

        [Test]
        public void ShouldReturnMethodInfoForFunctionInBodyNoArgument()
        {
            int x = 5;
            var methodInfo = MethodInfoUtil.GetMethodInfo(() => x.ToString());
            Assert.NotNull(methodInfo);
            Assert.AreEqual("ToString", methodInfo.Name);
            Assert.AreEqual(typeof(string), methodInfo.ReturnType);
        }

        [Test]
        public void ShouldReturnMethodInfoForStaticMethod()
        {
            var methodInfo = MethodInfoUtil.GetMethodInfo(() => StaticMethod());
            Assert.NotNull(methodInfo);
            Assert.AreEqual("StaticMethod", methodInfo.Name);
            Assert.AreEqual(typeof(void), methodInfo.ReturnType);
        }

        [Test]
        public void ShouldThrowArgumentExceptionForObjectCreationInBody()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                var methodInfo = MethodInfoUtil.GetMethodInfo<DummyObject.Factory>(
                    factory => new DummyObject.Factory());
            });
        }
    }
}

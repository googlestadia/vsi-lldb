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
using NUnit.Framework;
using System;
using System.Collections.Generic;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.Test.TestSupport
{
    [TestFixture]
    class RemoteValueFakeTests
    {
        string GetExpressionPath(RemoteValueFake remoteValue)
        {
            string expressionPath;
            Assert.True(remoteValue.GetExpressionPath(out expressionPath));
            return expressionPath;
        }

        [Test]
        public void CreateValueFromExpressionWhenExpressionNotFound()
        {
            var value = new RemoteValueFake("_myVar", "17");
            var exprValue = value.CreateValueFromExpression("name", "dummyExpression");

            Assert.That(exprValue.GetName(), Is.EqualTo(""));
            Assert.That(exprValue.GetDefaultValue(), Is.EqualTo(""));
            Assert.That(exprValue.GetError().Fail, Is.True);
        }

        [Test]
        public void CreateValueFromExpressionWhenExpressionFound()
        {
            var value = new RemoteValueFake("_myVar", "17");

            var exprValue = new RemoteValueFake("$23", "Q");
            exprValue.SetTypeInfo(new SbTypeStub("char", TypeFlags.IS_SCALAR));
            value.AddValueFromExpression("dummyExpression", exprValue);

            var exprResult = value.CreateValueFromExpression("exprName", "dummyExpression");

            Assert.That(exprResult.GetName(), Is.EqualTo("exprName"));
            Assert.That(exprResult.GetTypeName(), Is.EqualTo("char"));
            Assert.That(exprResult.GetDefaultValue(), Is.EqualTo("Q"));
        }

        [Test]
        public void CantAddNullChild()
        {
            var value = new RemoteValueFake("_myVar", "17");

            Assert.Throws<ArgumentException>(() => value.AddChild(null));
        }

        [Test]
        public void CantAddChildMoreThanOnce()
        {
            var value = new RemoteValueFake("_myVar", "17");
            var child = new RemoteValueFake("_myChild", "23");

            value.AddChild(child);

            Assert.Throws<InvalidOperationException>(() => value.AddChild(child));
        }

        [Test]
        public void CantAddChildToItself()
        {
            var value = new RemoteValueFake("_myVar", "17");

            Assert.Throws<InvalidOperationException>(() => value.AddChild(value));
        }

        [Test]
        public void GetChildrenWhenOutOfBounds()
        {
            var value = new RemoteValueFake("_myVar", "17");
            value.AddChild(new RemoteValueFake("child", "pi/e"));

            List<RemoteValue> children = value.GetChildren(0, 3);
            Assert.That(children[0].GetDefaultValue(), Is.EqualTo("pi/e"));
            Assert.That(children[1], Is.Null);
        }

        [Test]
        public void GetValueForExpressionStructPointerMismatch()
        {
            var list = new RemoteValueFake("list", "");
            list.SetTypeInfo(new SbTypeStub("CustomListType", TypeFlags.IS_CLASS));

            list.AddChild(new RemoteValueFake("size", "17"));

            Assert.False(list.TypeIsPointerType());

            var value = list.GetValueForExpressionPath("->size");
            Assert.That(value.GetError().Fail());
        }

        [Test]
        public void GetValueForExpressionPointerStructMismatch()
        {
            var list = new RemoteValueFake("list", "");
            list.SetTypeInfo(
                new SbTypeStub("CustomListType*", TypeFlags.IS_CLASS | TypeFlags.IS_POINTER));
            list.AddChild(new RemoteValueFake("size", "17"));

            Assert.True(list.TypeIsPointerType());

            var value = list.GetValueForExpressionPath(".size");
            Assert.That(value.GetError().Fail());
        }

        [Test]
        public void GetValueForExpressionInvalidExpression([Values(".9", "size")] string path)
        {
            var list = new RemoteValueFake("list", "");
            list.SetTypeInfo(new SbTypeStub("CustomListType", TypeFlags.IS_CLASS));
            list.AddChild(new RemoteValueFake("size", "17"));

            Assert.False(list.TypeIsPointerType());

            var value = list.GetValueForExpressionPath(path);
            Assert.That(value.GetError().Fail());
        }

        [Test]
        public void GetValueForExpressionChildDoesntExist()
        {
            var list = new RemoteValueFake("list", "");
            list.SetTypeInfo(new SbTypeStub("CustomListType", TypeFlags.IS_CLASS));
            list.AddChild(new RemoteValueFake("size", "17"));

            Assert.False(list.TypeIsPointerType());

            var value = list.GetValueForExpressionPath("list->end");
            Assert.That(value.GetError().Fail());
        }

        [Test]
        public void GetValueForExpressionPathComplexPointerAndNonPointer()
        {
            var value_10 = new RemoteValueFake("value", "10");
            var value_20 = new RemoteValueFake("value", "20");

            var next = new RemoteValueFake("next", "");
            next.SetTypeInfo(
                new SbTypeStub("LinkedListNod*", TypeFlags.IS_CLASS | TypeFlags.IS_POINTER));
            next.AddChild(value_20);

            var head = new RemoteValueFake("head", "");
            head.SetTypeInfo(
                new SbTypeStub("LinkedListNode*", TypeFlags.IS_CLASS | TypeFlags.IS_POINTER));
            head.AddChild(next);
            head.AddChild(value_10);

            var list = new RemoteValueFake("list", "");
            list.SetTypeInfo(new SbTypeStub("LinkedList", TypeFlags.IS_CLASS));
            list.AddChild(head);

            var h = list.GetValueForExpressionPath(".head");
            var v10 = list.GetValueForExpressionPath(".head->value");
            var n = list.GetValueForExpressionPath(".head->next");
            var v20 = list.GetValueForExpressionPath(".head->next->value");

            Assert.That(h, Is.SameAs(head));
            Assert.That(v10, Is.SameAs(value_10));
            Assert.That(n, Is.SameAs(next));
            Assert.That(v20, Is.SameAs(value_20));
        }

        [Test]
        public void GetValueForExpressionPathSubscript()
        {
            var v0 = new RemoteValueFake("[0]", "10");
            var v1 = new RemoteValueFake("[1]", "20");
            var v2 = new RemoteValueFake("[2]", "30");

            var array = new RemoteValueFake("nums", "");
            array.SetTypeInfo(new SbTypeStub("int[3]", TypeFlags.IS_ARRAY));
            array.AddChild(v0);
            array.AddChild(v1);
            array.AddChild(v2);

            var rv0 = array.GetValueForExpressionPath("[0]");
            var rv1 = array.GetValueForExpressionPath("[1]");
            var rv2 = array.GetValueForExpressionPath("[2]");

            Assert.That(rv0, Is.SameAs(v0));
            Assert.That(rv1, Is.SameAs(v1));
            Assert.That(rv2, Is.SameAs(v2));
        }

        [Test]
        public void GetExpressionPathComplexPointerAndNonPointer()
        {
            var value_10 = new RemoteValueFake("value", "10");
            var value_20 = new RemoteValueFake("value", "20");

            var next = new RemoteValueFake("next", "");
            next.SetTypeInfo(
                new SbTypeStub("LinkedListNode*", TypeFlags.IS_CLASS | TypeFlags.IS_POINTER));
            next.AddChild(value_20);

            var head = new RemoteValueFake("head", "");
            head.SetTypeInfo(
                new SbTypeStub("LinkedListNode*", TypeFlags.IS_CLASS | TypeFlags.IS_POINTER));
            head.AddChild(next);
            head.AddChild(value_10);

            var list = new RemoteValueFake("list", "");
            list.SetTypeInfo(new SbTypeStub("LinkedList", TypeFlags.IS_CLASS));
            list.AddChild(head);

            Assert.That(GetExpressionPath(list), Is.EqualTo("list"));
            Assert.That(GetExpressionPath(head), Is.EqualTo("list.head"));
            Assert.That(GetExpressionPath(value_10), Is.EqualTo("list.head->value"));
            Assert.That(GetExpressionPath(value_20), Is.EqualTo("list.head->next->value"));
        }

        [Test]
        public void GetExpressionPathForSubscript()
        {
            var v0 = new RemoteValueFake("[0]", "10");
            var v1 = new RemoteValueFake("[1]", "20");
            var v2 = new RemoteValueFake("[2]", "30");

            var array = new RemoteValueFake("nums", "");
            array.AddChild(v0);
            array.AddChild(v1);
            array.AddChild(v2);

            Assert.That(GetExpressionPath(v0), Is.EqualTo("nums[0]"));
            Assert.That(GetExpressionPath(v1), Is.EqualTo("nums[1]"));
            Assert.That(GetExpressionPath(v2), Is.EqualTo("nums[2]"));
        }

        [Test]
        public void GetTypeName()
        {
            var remoteValue = new RemoteValueFake("varName", "value");
            remoteValue.SetTypeInfo(new SbTypeStub("TypeName", TypeFlags.NONE));

            Assert.That(remoteValue.GetTypeName(), Is.EqualTo("TypeName"));
        }

        [Test]
        public void IsPointerWhenNotAPointer()
        {
            var remoteValue = new RemoteValueFake("varName", "value");
            remoteValue.SetTypeInfo(new SbTypeStub("TypeName", TypeFlags.NONE));

            Assert.False(remoteValue.TypeIsPointerType());
        }

        [Test]
        public void IsPointerWhenIsAPointer()
        {
            var remoteValue = new RemoteValueFake("varName", "value");
            remoteValue.SetTypeInfo(new SbTypeStub("TypeName", TypeFlags.IS_POINTER));

            Assert.True(remoteValue.TypeIsPointerType());
        }

        [Test]
        public void IsPointerWhenIsAnInstancePointer()
        {
            var remoteValue = new RemoteValueFake("varName", "value");
            remoteValue.SetTypeInfo(new SbTypeStub("TypeName", TypeFlags.INSTANCE_IS_POINTER));

            Assert.True(remoteValue.TypeIsPointerType());
        }
    }
}

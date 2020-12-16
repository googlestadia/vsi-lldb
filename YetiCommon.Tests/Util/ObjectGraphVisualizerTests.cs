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
using YetiCommon.Util;
using static YetiCommon.Util.ObjectGraphVisualizer;

namespace YetiCommon.Tests.Util
{
    /// <summary>
    /// Quick and somewhat hacky tests for ObjectGraphVisualizer.
    /// </summary>
    ///
    /// <remarks>
    /// ***** IMPORTANT *****
    ///
    /// The style of these tests are very brittle because they are doing literal string comparisons.
    /// We should NOT add too many tests in this style.  Instead we should refactor the
    /// ObjectGraphVisualizer to be more easily tested by breaking apart the graph model from the
    /// serialization of it.
    /// </remarks>
    [TestFixture]
    class ObjectGraphVisualizerTests
    {
        class SimpleObj
        {
            // Prevent unused variable warning
#pragma warning disable CS0414
            int number = 23;
#pragma warning restore CS0414
        }

        class RootObj
        {
            public InteriorObj interiorObj = new InteriorObj();
        }

        class InteriorObj
        {
            public LeafObj leafObj = new LeafObj();
        }

        class LeafObj
        {
            // Prevent unused variable warning
#pragma warning disable CS0414
            object nullObj = null;
#pragma warning restore CS0414
        }

        class NodeObj
        {
            public NodeObj next;
        }

        class ArrayObj
        {
            public LeafObj[] array = null;
        }

        class MultiArrayObj
        {
            public LeafObj[,] multiArray = null;
        }

        [Test]
        public void VisualizeNullObject()
        {
            var visualizer = new ObjectGraphVisualizer();
            IData data;
            Assert.Throws<ArgumentNullException>(() => visualizer.Visualize(null, out data));
        }

        [Test]
        public void GraphSettings()
        {
            var visualizer = new ObjectGraphVisualizer();
            IData data;

            var root = new RootObj();

            var dot = visualizer.Visualize(root, out data);


            Assert.That(dot, Does.Contain("graph [splines=ortho]"));
            Assert.That(dot, Does.Contain("node [shape=box style=rounded]"));
        }

        [Test]
        public void VisualizeSimpleObjectWithPrimitives()
        {
            var visualizer = new ObjectGraphVisualizer();
            IData data;

            var root = new SimpleObj();

            var dot = visualizer.Visualize(root, out data);

            Assert.That(dot, Does.Contain(
                "1 [ label=\"YetiCommon.Tests.Util.SimpleObj\" id=\"dark googlegreen\" ];"));

            Assert.That(data.RootObject, Is.EqualTo(root));
            Assert.That(data.InternalNodes, Is.Empty);
            Assert.That(data.TruncatedNodes, Is.Empty);
            Assert.That(data.LeafNodes, Is.EquivalentTo(new object[] { root }));
        }

        [Test]
        public void VisualizeSimpleGraph()
        {
            var visualizer = new ObjectGraphVisualizer();
            IData data;

            var root = new RootObj();

            var dot = visualizer.Visualize(root, out data);

            Assert.That(dot, Does.Contain(
                "1 [ label=\"YetiCommon.Tests.Util.RootObj\" id=\"dark googlegreen\" ];"));
            Assert.That(dot, Does.Contain("2 [ label=\"YetiCommon.Tests.Util.InteriorObj\" ];"));
            Assert.That(dot, Does.Contain(
                "3 [ label=\"YetiCommon.Tests.Util.LeafObj\" id=\"dark googlered\" ];"));

            Assert.That(dot, Does.Contain("1 -> 2 [ label=\"interiorObj\" ];"));
            Assert.That(dot, Does.Contain("2 -> 3 [ label=\"leafObj\" ];"));

            Assert.That(data.RootObject, Is.EqualTo(root));
            Assert.That(data.InternalNodes,
                Is.EquivalentTo(new object[] { root, root.interiorObj }));
            Assert.That(data.TruncatedNodes, Is.Empty);
            Assert.That(data.LeafNodes, Is.EquivalentTo(new object[] { root.interiorObj.leafObj }));
        }

        [Test]
        public void InspectObjDelegate()
        {
            var visualizer = new ObjectGraphVisualizer(
                o => { return o.Object.GetType().Equals(typeof(RootObj)); });
            IData data;

            var root = new RootObj();

            var dot = visualizer.Visualize(root, out data);

            Assert.That(dot, Does.Contain(
                "1 [ label=\"YetiCommon.Tests.Util.RootObj\" id=\"dark googlegreen\" ];"));
            Assert.That(dot, Does.Contain(
                "2 [ label=\"YetiCommon.Tests.Util.InteriorObj\" id=\"googlered\" ];"));
            Assert.That(dot, Does.Not.Contain("YetiCommon.Tests.Util.LeafObj"));

            Assert.That(dot, Does.Contain("1 -> 2 [ label=\"interiorObj\" ];"));
            Assert.That(dot, Does.Not.Contain("2 -> 3"));

            Assert.That(data.RootObject, Is.EqualTo(root));
            Assert.That(data.InternalNodes, Is.EquivalentTo(new object[] { root }));
            Assert.That(data.TruncatedNodes, Is.EquivalentTo(new object[] { root.interiorObj }));
            Assert.That(data.LeafNodes, Is.Empty);
        }

        [Test]
        public void VisualizeGraphWithCycle()
        {
            var visualizer = new ObjectGraphVisualizer();
            IData data;

            var root = new NodeObj();
            root.next = new NodeObj();
            root.next.next = root;

            var dot = visualizer.Visualize(root, out data);

            Assert.That(dot, Does.Contain(
                "1 [ label=\"YetiCommon.Tests.Util.NodeObj\" id=\"dark googlegreen\" ];"));
            Assert.That(dot, Does.Contain("2 [ label=\"YetiCommon.Tests.Util.NodeObj\" ];"));

            Assert.That(dot, Does.Contain("1 -> 2 [ label=\"next\" ];"));
            Assert.That(dot, Does.Contain("2 -> 1 [ label=\"next\" ];"));

            Assert.That(data.RootObject, Is.EqualTo(root));
            Assert.That(data.InternalNodes, Is.EquivalentTo(new object[] { root, root.next }));
            Assert.That(data.TruncatedNodes, Is.Empty);
            Assert.That(data.LeafNodes, Is.Empty);
        }

        [Test]
        public void VisualizeGraphWithNullArray()
        {
            var visualizer = new ObjectGraphVisualizer();
            IData data;

            var root = new ArrayObj();
            root.array = null;

            var dot = visualizer.Visualize(root, out data);

            Assert.That(dot, Does.Contain
                ("1 [ label=\"YetiCommon.Tests.Util.ArrayObj\" id=\"dark googlegreen\" ];"));

            Assert.That(data.RootObject, Is.EqualTo(root));
            Assert.That(data.InternalNodes, Is.Empty);
            Assert.That(data.TruncatedNodes, Is.Empty);
            Assert.That(data.LeafNodes, Is.EquivalentTo(new object[] { root }));
        }

        [Test]
        public void VisualizeGraphWithArray()
        {
            var visualizer = new ObjectGraphVisualizer();
            IData data;

            var root = new ArrayObj();
            root.array = new LeafObj[] { new LeafObj(), new LeafObj() };

            var dot = visualizer.Visualize(root, out data);

            Assert.That(dot, Does.Contain(
                "1 [ label=\"YetiCommon.Tests.Util.ArrayObj\" id=\"dark googlegreen\" ];"));
            Assert.That(dot, Does.Contain(
                "2 [ label=\"YetiCommon.Tests.Util.LeafObj\" id=\"dark googlered\" ];"));
            Assert.That(dot, Does.Contain(
                "3 [ label=\"YetiCommon.Tests.Util.LeafObj\" id=\"dark googlered\" ];"));

            Assert.That(dot, Does.Contain("1 -> 2 [ label=\"array[0]\" ];"));
            Assert.That(dot, Does.Contain("1 -> 3 [ label=\"array[1]\" ];"));

            Assert.That(data.RootObject, Is.EqualTo(root));
            Assert.That(data.InternalNodes, Is.EquivalentTo(new object[] { root }));
            Assert.That(data.TruncatedNodes, Is.Empty);
            Assert.That(data.LeafNodes,
                Is.EquivalentTo(new object[] { root.array[0], root.array[1] }));
        }

        [Test]
        public void VisualizeGraphWithMultiDimensionalArray()
        {
            var visualizer = new ObjectGraphVisualizer();
            IData data;

            var root = new MultiArrayObj();

            root.multiArray = new LeafObj[,]
            {
                { new LeafObj(), new LeafObj() },
                { new LeafObj(), new LeafObj() }
            };

            var dot = visualizer.Visualize(root, out data);

            Assert.That(dot, Does.Contain(
                "1 [ label=\"YetiCommon.Tests.Util.MultiArrayObj\" id=\"dark googlegreen\" ];"));
            Assert.That(dot, Does.Contain(
                "2 [ label=\"YetiCommon.Tests.Util.LeafObj\" id=\"dark googlered\" ];"));
            Assert.That(dot, Does.Contain(
                "3 [ label=\"YetiCommon.Tests.Util.LeafObj\" id=\"dark googlered\" ];"));
            Assert.That(dot, Does.Contain(
                "4 [ label=\"YetiCommon.Tests.Util.LeafObj\" id=\"dark googlered\" ];"));
            Assert.That(dot, Does.Contain(
                "5 [ label=\"YetiCommon.Tests.Util.LeafObj\" id=\"dark googlered\" ];"));

            Assert.That(dot, Does.Contain("1 -> 2 [ label=\"multiArray[0, 0]\" ];"));
            Assert.That(dot, Does.Contain("1 -> 3 [ label=\"multiArray[0, 1]\" ];"));
            Assert.That(dot, Does.Contain("1 -> 4 [ label=\"multiArray[1, 0]\" ];"));
            Assert.That(dot, Does.Contain("1 -> 5 [ label=\"multiArray[1, 1]\" ];"));

            Assert.That(data.RootObject, Is.EqualTo(root));
            Assert.That(data.InternalNodes, Is.EquivalentTo(new object[] { root }));
            Assert.That(data.TruncatedNodes, Is.Empty);
            Assert.That(data.LeafNodes, Is.EquivalentTo(new object[] {
                root.multiArray[0, 0], root.multiArray[0, 1],
                root.multiArray[1, 0], root.multiArray[1, 1] }));
        }
    }
}

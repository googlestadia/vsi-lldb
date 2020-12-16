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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace YetiCommon.Util
{
    /// <summary>
    /// Utility class to visualize an object graph in DOT graph format.
    ///
    /// Based on Jackson Dunstan, https://JacksonDunstan.com/articles/5034
    /// </summary>
    ///
    /// <license>
    /// MIT
    /// </license>
    public class ObjectGraphVisualizer
    {
        /// <summary>
        /// Callback used to determine preemptive stopping conditions when traversing the object
        /// graph.
        /// </summary>
        ///
        /// <param name="ctx">The current traversal context.</param>
        ///
        /// <returns>True if graph traversal should continue.</returns>
        public delegate bool InspectObjectDelegate(InspectObjectContext ctx);

        /// <summary>
        /// Context used by InspectObject delegate.
        /// </summary>
        public class InspectObjectContext
        {
            public InspectObjectContext(object obj)
            {
                Object = obj;
            }

            /// <summary>
            /// The current object being inspected.
            /// </summary>
            public readonly object Object;
        }

        /// <summary>
        /// Analyzed graph data output from Visualize().
        /// </summary>
        public interface IData
        {
            object RootObject { get; }
            List<object> AllNodes { get; }
            List<object> InternalNodes { get; }
            List<object> TruncatedNodes { get; }
            List<object> LeafNodes { get; }

            /// <summary>
            /// Returns the number of objects that have a reference to a given object.
            /// </summary>
            int GetInLinkCount(object obj);
            int GetOutLinkCount(object obj);

            /// <summary>
            /// Get a dictionary that maps all the types found in the graph all the object instances
            /// that are of that type, or some derivative of it.
            /// </summary>
            Dictionary<Type, List<object>> GetMultiInstances();

            /// <summary>
            /// Get the list of objects that are of a given type or derive from it.
            /// </summary>
            List<object> GetInstances(Type type);
        }

        class DataImpl : IData
        {
            object rootObject = null;
            List<object> internalNodes = new List<object>();
            List<object> truncatedNodes = new List<object>();
            List<object> leafNodes = new List<object>();

            public DataImpl(object rootObject)
            {
                DataNodes = new Dictionary<object, Node>(1024);
                this.rootObject = rootObject;
            }

            #region IData

            public Dictionary<object, Node> DataNodes;
            public object RootObject => rootObject;
            public List<object> AllNodes => DataNodes.Keys.ToList();
            public List<object> InternalNodes => internalNodes;
            public List<object> TruncatedNodes => truncatedNodes;
            public List<object> LeafNodes => leafNodes;

            public int GetInLinkCount(object obj)
            {
                Node node;
                if (!DataNodes.TryGetValue(obj, out node))
                {
                    throw new ArgumentException($"{nameof(obj)} does not exist in the graph.");
                }
                return node.InLinks.Count();
            }

            public int GetOutLinkCount(object obj)
            {
                Node node;
                if (!DataNodes.TryGetValue(obj, out node))
                {
                    throw new ArgumentException($"{nameof(obj)} does not exist in the graph.");
                }
                return node.OutLinks.Count();
            }

            public Dictionary<Type, List<object>> GetMultiInstances()
            {
                var multiInstances = new Dictionary<Type, List<object>>();
                var distinctTypes = DataNodes.Keys.Select(o => o.GetType())
                    .Where(t => t != typeof(object)).Distinct();
                foreach (var type in distinctTypes)
                {
                    var instances = GetInstances(type);
                    if (instances.Count > 1)
                    {
                        multiInstances[type] = instances;
                    }
                }
                return multiInstances;
            }

            public List<object> GetInstances(Type type)
            {
                return AllNodes.FindAll(o => type.IsAssignableFrom(o.GetType())).ToList();
            }

            #endregion
        }

        /// <summary>
        /// Defines the traversal stopping conditions.
        /// </summary>
        private readonly InspectObjectDelegate inspectObj;

        /// <summary>
        /// Constructs a new object graph visualizer that will traverse the entire object graph.
        /// </summary>
        public ObjectGraphVisualizer() : this(null) { }

        /// <summary>
        /// Constructs a new object graph visualizer.
        /// </summary>
        ///
        /// <param name="inspectObj">
        /// Callback used to determine when to stop traversing the object graph. The entire graph
        /// will be traversed when null.
        /// </param>
        public ObjectGraphVisualizer(InspectObjectDelegate inspectObj)
        {
            if (inspectObj == null)
            {
                inspectObj = o => { return true; };
            }
            this.inspectObj = inspectObj;
        }

        /// <summary>
        /// A node of the graph
        /// </summary>
        private sealed class Node
        {
            /// <summary>
            /// The object the node represents
            /// </summary>
            public readonly object Object;

            /// <summary>
            /// Links from other nodes to the node. Keys are field names. Values are node IDs.
            /// </summary>
            public readonly Dictionary<string, int> InLinks;

            /// <summary>
            /// Links from the node to other nodes. Keys are field names. Values are node IDs.
            /// </summary>
            public readonly Dictionary<string, int> OutLinks;

            /// <summary>
            /// ID of the node. Unique to its graph.
            /// </summary>
            public readonly int Id;

            /// <summary>
            /// True if this node is root of the graph.
            /// </summary>
            public readonly bool IsRoot;

            /// <summary>
            /// True if the graph traversal stopped on this node based on the |inpsectObj| callback.
            /// </summary>
            public bool WasTruncated = false;

            /// <summary>
            /// Create a node
            /// </summary>
            ///
            /// <param name="obj">
            /// The object the node represents.
            /// </param>
            ///
            /// <param name="id">
            /// ID of the node. Must be unique to its graph.
            /// </param>
            public Node(object obj, int id, bool isRoot = false)
            {
                Object = obj;
                InLinks = new Dictionary<string, int>(16);
                OutLinks = new Dictionary<string, int>(16);
                Id = id;
                IsRoot = isRoot;
            }
        }

        /// <summary>
        /// Add a node to a graph to represent an object
        /// </summary>
        ///
        /// <returns>
        /// The added node or the existing node if one already exists for the object
        /// </returns>
        ///
        /// <param name="nodes">
        /// Graph to add to
        /// </param>
        ///
        /// <param name="obj">
        /// Object to add a node for
        /// </param>
        ///
        /// <param name="tempBuilder">
        /// String builder to use only temporarily
        /// </param>
        ///
        /// <param name="nextNodeId">
        /// ID to assign to the next node. Incremented after assignment.
        /// </param>
        ///
        /// <param name="isRoot">
        /// True when the node represents the object graphs root.
        /// </param>
        private Node AddObject(
            Dictionary<object, Node> nodes,
            object obj,
            StringBuilder tempBuilder,
            ref int nextNodeId,
            bool isRoot = false)
        {
            // Check if there is already a node for the object
            Node node;
            if (nodes.TryGetValue(obj, out node))
            {
                return node;
            }

            // Add a node for the object
            Type objType = obj.GetType();
            node = new Node(obj, nextNodeId, isRoot);
            nextNodeId++;
            nodes.Add(obj, node);

            if (!inspectObj(new InspectObjectContext(obj)))
            {
                node.WasTruncated = true;
                return node;
            }

            // Add linked nodes for all fields
            foreach (FieldInfo fieldInfo in EnumerateInstanceFieldInfos(objType))
            {
                // Only add reference types
                Type fieldType = fieldInfo.FieldType;
                if (!fieldType.IsPointer && !IsUnmanagedType(fieldType))
                {
                    object field = fieldInfo.GetValue(obj);
                    if (fieldType.IsArray)
                    {
                        LinkArray(
                            nodes,
                            node,
                            (Array)field,
                            fieldInfo.Name,
                            tempBuilder,
                            ref nextNodeId);
                    }
                    else
                    {
                        LinkNode(
                            nodes,
                            node,
                            field,
                            fieldInfo.Name,
                            tempBuilder,
                            ref nextNodeId);
                    }
                }
            }
            return node;
        }

        /// <summary>
        /// Add new linked nodes for the elements of an array
        /// </summary>
        ///
        /// <param name="nodes">
        /// Graph to add to
        /// </param>
        ///
        /// <param name="node">
        /// Node to link from
        /// </param>
        ///
        /// <param name="array">
        /// Array whose elements should be linked
        /// </param>
        ///
        /// <param name="arrayName">
        /// Name of the array field
        /// </param>
        ///
        /// <param name="tempBuilder">
        /// String builder to use only temporarily
        /// </param>
        ///
        /// <param name="nextNodeId">
        /// ID to assign to the next node. Incremented after assignment.
        /// </param>
        private void LinkArray(
            Dictionary<object, Node> nodes,
            Node node,
            Array array,
            string arrayName,
            StringBuilder tempBuilder,
            ref int nextNodeId)
        {
            // Don't link null arrays
            if (ReferenceEquals(array, null))
            {
                return;
            }

            // Create an array of lengths of each rank
            int rank = array.Rank;
            int[] lengths = new int[rank];
            for (int i = 0; i < lengths.Length; ++i)
            {
                lengths[i] = array.GetLength(i);
            }

            // Create an array of indices into each rank
            int[] indices = new int[rank];
            indices[rank - 1] = -1;

            // Iterate over all elements of all ranks
            while (true)
            {
                // Increment the indices
                for (int i = rank - 1; i >= 0; --i)
                {
                    indices[i]++;

                    // No overflow, so we can link
                    if (indices[i] < lengths[i])
                    {
                        goto link;
                    }

                    // Overflow, so carry.
                    indices[i] = 0;
                }
                break;

                link:
                // Build the field name: "name[1, 2, 3]"
                tempBuilder.Length = 0;
                tempBuilder.Append(arrayName);
                tempBuilder.Append('[');
                for (int i = 0; i < indices.Length; ++i)
                {
                    tempBuilder.Append(indices[i]);
                    if (i != indices.Length - 1)
                    {
                        tempBuilder.Append(", ");
                    }
                }
                tempBuilder.Append(']');

                // Link the element as a node
                object element = array.GetValue(indices);
                string elementName = tempBuilder.ToString();
                LinkNode(
                    nodes,
                    node,
                    element,
                    elementName,
                    tempBuilder,
                    ref nextNodeId);
            }
        }

        /// <summary>
        /// Add a new linked node
        /// </summary>
        ///
        /// <param name="nodes">
        /// Graph to add to
        /// </param>
        ///
        /// <param name="node">
        /// Node to link from
        /// </param>
        ///
        /// <param name="obj">
        /// Object to link a node for
        /// </param>
        ///
        /// <param name="name">
        /// Name of the object
        /// </param>
        ///
        /// <param name="tempBuilder">
        /// String builder to use only temporarily
        /// </param>
        ///
        /// <param name="nextNodeId">
        /// ID to assign to the next node. Incremented after assignment.
        /// </param>
        private void LinkNode(
            Dictionary<object, Node> nodes,
            Node node,
            object obj,
            string name,
            StringBuilder tempBuilder,
            ref int nextNodeId)
        {
            // Don't link null objects
            if (ReferenceEquals(obj, null))
            {
                return;
            }

            // Add a node for the object
            Node linkedNode = AddObject(nodes, obj, tempBuilder, ref nextNodeId);
            node.OutLinks[name] = linkedNode.Id;
            linkedNode.InLinks[name] = node.Id;
        }

        /// <summary>
        /// Check if a type is unmanaged, i.e. isn't and contains no managed types at any level of
        /// nesting.
        /// </summary>
        ///
        /// <returns>
        /// Whether the given type is unmanaged or not
        /// </returns>
        ///
        /// <param name="type">
        /// Type to check
        /// </param>
        private bool IsUnmanagedType(Type type)
        {
            if (!type.IsValueType)
            {
                return false;
            }
            if (type.IsPrimitive || type.IsEnum)
            {
                return true;
            }
            foreach (FieldInfo field in EnumerateInstanceFieldInfos(type))
            {
                if (!IsUnmanagedType(field.FieldType))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Enumerate the instance fields of a type and all its base types
        /// </summary>
        ///
        /// <returns>
        /// The fields of the given type and all its base types
        /// </returns>
        ///
        /// <param name="type">
        /// Type to enumerate
        /// </param>
        private IEnumerable<FieldInfo> EnumerateInstanceFieldInfos(Type type)
        {
            const BindingFlags bindingFlags =
                BindingFlags.Instance
                | BindingFlags.NonPublic
                | BindingFlags.Public;
            while (type != null)
            {
                foreach (FieldInfo fieldInfo in type.GetFields(bindingFlags))
                {
                    yield return fieldInfo;
                }
                type = type.BaseType;
            }
        }

        /// <summary>
        /// Visualize the given object by generating DOT which can be rendered with GraphViz.
        /// </summary>
        ///
        /// <example>
        ///
        /// // 1) Generate a DOT file for an object
        /// File.WriteAllText("object.dot", new ObjectGraphVisualizer().Visualize(obj));
        ///
        /// // 2) Render a graph for the using (internal).
        ///
        /// <returns>
        /// DOT, which can be rendered with GraphViz
        /// </returns>
        ///
        /// <param name="obj">
        /// Object to visualize
        /// </param>
        ///
        /// <param name="data">
        /// Analyzed graph data.
        /// </param>
        public string Visualize(object obj, out IData data)
        {
            // Build the graph
            DataImpl dataImpl = new DataImpl(obj);
            data = dataImpl;
            int nextNodeId = 1;
            StringBuilder tempBuilder = new StringBuilder(64);
            AddObject(dataImpl.DataNodes, obj, tempBuilder, ref nextNodeId, true);

            return CreateDOT(dataImpl.DataNodes, dataImpl);
        }

        /// <summary>
        /// Generates DOT graph serialization for a graph.
        /// </summary>
        /// <param name="nodes">The node graph.</param>
        /// <param name="data">Output data to populate</param>
        /// <returns>DOT graph string.</returns>
        private string CreateDOT(Dictionary<object, Node> nodes, DataImpl data)
        {
            StringBuilder output = new StringBuilder(1024 * 64);

            // Write the header
            output.Append("digraph\n");
            output.Append("{\n");


            output.Append("    graph [splines=ortho]\n");
            output.Append("    node [shape=box style=rounded]\n");

            // Write the mappings from ID to label
            foreach (Node node in nodes.Values)
            {
                output.Append("    ");
                output.Append(node.Id);
                output.Append(" [ label=\"");
                output.Append($"{node.Object.GetType().Namespace}.{node.Object.GetType().Name}");
                output.Append("\"");

                if (node.WasTruncated)
                {
                    data.TruncatedNodes.Add(node.Object);
                    output.Append(" id=\"googlered\"");
                }
                else if (node.IsRoot)
                {
                    if (node.OutLinks.Any())
                    {
                        data.InternalNodes.Add(node.Object);
                    }
                    else
                    {
                        data.LeafNodes.Add(node.Object);
                    }
                    output.Append(" id=\"dark googlegreen\"");
                }
                else if (!node.OutLinks.Any())
                {
                    data.LeafNodes.Add(node.Object);
                    output.Append(" id=\"dark googlered\"");
                }
                else
                {
                    data.InternalNodes.Add(node.Object);
                }

                output.Append(" ];\n");
            }

            // Write the node connections
            foreach (Node node in nodes.Values)
            {
                foreach (KeyValuePair<string, int> pair in node.OutLinks)
                {
                    output.Append("    ");
                    output.Append(node.Id);
                    output.Append(" -> ");
                    output.Append(pair.Value);
                    output.Append(" [ label=\"");
                    output.Append(pair.Key);
                    output.Append("\" ];\n");
                }
            }

            // Write the footer
            output.Append("}\n");

            return output.ToString();
        }
    }
}

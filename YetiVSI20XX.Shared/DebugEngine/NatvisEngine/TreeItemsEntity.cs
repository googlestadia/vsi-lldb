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

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DebuggerApi;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.DebugEngine.NatvisEngine
{
    public class TreeItemsEntity : LeafEntity
    {
        public class Factory
        {
            readonly NatvisDiagnosticLogger _logger;
            readonly NatvisExpressionEvaluator _evaluator;
            readonly NatvisSizeParser _sizeParser;

            [Obsolete("This constructor only exists to support mocking libraries.", error: true)]
            public Factory()
            {
            }

            public Factory(NatvisDiagnosticLogger logger, NatvisExpressionEvaluator evaluator,
                           NatvisSizeParser sizeParser)
            {
                _logger = logger;
                _evaluator = evaluator;
                _sizeParser = sizeParser;
            }

            public INatvisEntity Create(IVariableInformation variable, NatvisScope natvisScope,
                                        TreeItemsType treeItems) =>
                new TreeItemsEntity(variable, natvisScope, treeItems, _logger,
                                    new NatvisEntityStore(), _evaluator, _sizeParser);
        }

        readonly TreeItemsType _treeItems;
        readonly NatvisEntityStore _store;
        readonly NatvisSizeParser _sizeParser;

        readonly Stack<IVariableInformation> _nodeStack = new Stack<IVariableInformation>();

        bool _initialized;
        bool _sizeDefined;
        ErrorVariableInformation _nodeError;
        int _lastIndex;

        protected override string IncludeView => _treeItems.IncludeView;
        protected override string ExcludeView => _treeItems.ExcludeView;
        protected override string Condition => _treeItems.Condition;
        protected override bool Optional => _treeItems.Optional;
        protected override string VisualizerName => "<TreeItems>";

        TreeItemsEntity(IVariableInformation variable, NatvisScope natvisScope,
                        TreeItemsType treeItems, NatvisDiagnosticLogger logger,
                        NatvisEntityStore store, NatvisExpressionEvaluator evaluator,
                        NatvisSizeParser sizeParser)
            : base(variable, logger, evaluator, natvisScope)
        {
            _treeItems = treeItems;
            _store = store;
            _sizeParser = sizeParser;
        }

        #region INatvisEntity functions

        public override async Task<int> CountChildrenAsync()
        {
            await InitAsync();
            return _store.ValidationError == null ? _store.ChildrenCount : 1;
        }

        public override async Task<IList<IVariableInformation>> GetChildrenAsync(
            int from, int count)
        {
            await InitAsync();
            if (_store.ValidationError != null)
            {
                return new List<IVariableInformation>() {_store.ValidationError}.GetRange(
                    from, count);
            }

            await TraverseAndEvaluateUpToAsync(from + count);
            return Enumerable.Range(from, count).Select(i => _store.GetVariable(i)).ToList();
        }

        public override async Task<bool> IsValidAsync()
        {
            await InitAsync();
            return _store.ValidationError == null;
        }

        #endregion

        protected override void OnChildrenLimitIncreased()
        {
            if (!_sizeDefined)
            {
                _initialized = false;
            }
        }

        // TODO Verify <ValueNode>, <RightPointer> expression at least one time
        // in Validate method for TreeItems.
        protected override async Task ValidateAsync()
        {
            if (string.IsNullOrEmpty(_treeItems.HeadPointer))
            {
                throw new InvalidOperationException("No valid <HeadPointer> found.");
            }

            if (string.IsNullOrEmpty(_treeItems.LeftPointer))
            {
                throw new InvalidOperationException("No valid <LeftPointer> found.");
            }

            if (string.IsNullOrEmpty(_treeItems.RightPointer))
            {
                throw new InvalidOperationException("No valid <RightPointer> found.");
            }

            if (string.IsNullOrEmpty(_treeItems?.ValueNode.Value))
            {
                throw new InvalidOperationException("No valid <ValueNode> found.");
            }

            if (_lastIndex == 0 && _nodeStack.Count == 0)
            {
                await InitNodeStackAsync();
            }
        }

        protected override async Task<int> InitChildrenCountAsync()
        {
            if (_treeItems.Size != null)
            {
                IVariableInformation sizeVariable = await _evaluator.EvaluateExpressionAsync(
                    _treeItems.Size, _variable, _natvisScope, null);

                sizeVariable.FallbackValueFormat = ValueFormat.Default;

                uint size;
                _sizeDefined = _sizeParser.ParseUint(await sizeVariable.ValueAsync(), out size);
                if (!_sizeDefined)
                {
                    throw new ExpressionEvaluationFailed(
                        "Failed to evaluate <Size> node. The expression's value " +
                        $"was not a number. Expression='{_treeItems.Size}' " +
                        $"Value='{await sizeVariable.ValueAsync()}'");
                }

                return (int) size;
            }

            return await EvaluateChildrenCountAsync();
        }

        async Task InitAsync()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            EntityInfo initInfo = await InitLeafAsync();
            _store.ChildrenCount = initInfo.ChildrenCount;
            _store.ValidationError = initInfo.Error;
        }

        async Task<int> EvaluateChildrenCountAsync()
        {
            while (_nodeStack.Count > 0 && _lastIndex < ChildrenLimit && _nodeError == null)
            {
                await EvaluateNextInOrderAsync();
            }

            // Make sure error is reported if AddNodesUpToNextInOrderToNodeStack fails.
            if (_lastIndex < ChildrenLimit && _nodeError != null)
            {
                await EvaluateNextInOrderAsync();
            }

            return _lastIndex;
        }

        // TODO Support fast pass for TreeItems Natvis element.
        async Task TraverseAndEvaluateUpToAsync(int to)
        {
            while (_lastIndex < to)
            {
                await EvaluateNextInOrderAsync();
            }
        }

        /// <summary>
        /// This function evaluates next tree node and caches the value.
        /// There are several scenarios:
        /// - _nodeStack is not empty. It means there are still nodes to evaluate. In that case
        /// the top node in stack is popped and evaluated, and then we decide what is the next
        /// node by invoking AddNodesUpToNextInOrderToNodeStack().
        /// - _nodeStack is empty. It means that more nodes are requested than exist in the tree,
        /// in that case we return an error node with a message about wrong size.
        /// - We encountered an error on one of the previous stages. In that case we just return
        /// the same error.
        /// </summary>
        async Task EvaluateNextInOrderAsync()
        {
            if (_nodeError != null)
            {
                _store.SaveVariable(_lastIndex, _nodeError);
            }
            else if (_nodeStack.Count > 0)
            {
                IVariableInformation currentNode = _nodeStack.Pop();
                _store.SaveVariable(_lastIndex,
                                    await GetDisplayVariableAsync(_lastIndex, currentNode));

                await AddNodesUpToNextInOrderToNodeStackAsync(currentNode);
            }
            else
            {
                if (_sizeDefined)
                {
                    int size = _store.ChildrenCount;
                    _logger.Warning("<TreeItems> declared a size of " +
                                    $"{size} but only {_lastIndex} item(s) found.");

                    _nodeError = new ErrorVariableInformation(
                        "<Error>", $"Size declared as {size} but only {_lastIndex} item(s) found.");
                }
                else
                {
                    _nodeError = new ErrorVariableInformation(
                        "<Error>", $"Item {_lastIndex} is out of bound.");
                }

                _store.SaveVariable(_lastIndex, _nodeError);
            }

            _lastIndex++;
        }

        /// <summary>
        /// This function is doing pre-initialization for in-order traversal. It retrieves the
        /// root of the tree and finds it's left most child, which is the first element for in-order
        /// traversal. If there is no left sub-tree, the first element will be the root of the tree.
        /// </summary>
        async Task InitNodeStackAsync()
        {
            IVariableInformation treeRoot;
            if (_treeItems.HeadPointer == "this")
            {
                treeRoot = _variable;
            }
            else
            {
                treeRoot = await _evaluator.EvaluateExpressionAsync(_treeItems.HeadPointer,
                                                                    _variable, _natvisScope, null);
            }

            await AddNodesUpToLeftMostToNodeStackAsync(treeRoot);
        }

        /// <summary>
        /// This functions figures out what is the next node to be evaluated after currentNode
        /// using in-order traversal.
        /// At each step, we try to find the left most child of the current node in the right
        /// sub-tree and adding all the nodes we traverse to the _nodesStack. The last node added
        /// will be the left most node. If there are no right sub-tree, nothing is added to the
        /// _nodesStack.
        /// </summary>
        async Task AddNodesUpToNextInOrderToNodeStackAsync(IVariableInformation currentNode)
        {
            try
            {
                IVariableInformation nextNode = await _evaluator.EvaluateExpressionAsync(
                    _treeItems.RightPointer, currentNode, _natvisScope, null);

                await AddNodesUpToLeftMostToNodeStackAsync(nextNode);
            }
            catch (ExpressionEvaluationFailed ex)
            {
                _nodeError = Optional
                    ? new ErrorVariableInformation("<Warning>", ex.Message)
                    : new ErrorVariableInformation("<Error>", ex.Message);
            }
        }

        /// <summary>
        /// Adds nodes starting from the provided node till the left most node to _nodeStack.
        /// Stops when null pointer is encountered. May throw ExpressionEvaluationFailed if unable
        /// to evaluate LeftPointer.
        /// </summary>
        async Task AddNodesUpToLeftMostToNodeStackAsync(IVariableInformation node)
        {
            while (!node.IsNullPointer())
            {
                _nodeStack.Push(node);
                node = await _evaluator.EvaluateExpressionAsync(_treeItems.LeftPointer, node,
                                                                _natvisScope, null);
            }
        }

        async Task<IVariableInformation> GetDisplayVariableAsync(
            int index, IVariableInformation node)
        {
            string elementName = $"[{index}]";

            if (_treeItems.ValueNode.Value == "this")
            {
                return new NamedVariableInformation(node, elementName);
            }

            return await _evaluator.GetExpressionValueOrErrorAsync(
                _treeItems.ValueNode.Value, node, _natvisScope, elementName, "TreeItems");
        }
    }
}
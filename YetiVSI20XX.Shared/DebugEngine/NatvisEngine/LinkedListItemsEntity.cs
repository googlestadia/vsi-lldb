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
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.DebugEngine.NatvisEngine
{
    // TODO: Support fast skip and count operations.
    public class LinkedListItemsEntity : LeafEntity
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
                                        LinkedListItemsType linkedListItems) =>
                new LinkedListItemsEntity(variable, natvisScope, linkedListItems, _logger,
                                          new NatvisEntityStore(), _evaluator, _sizeParser);
        }

        readonly LinkedListItemsType _linkedListItems;
        readonly NatvisEntityStore _store;
        readonly NatvisSizeParser _sizeParser;

        bool _initialized;

        int _lastIndex;

        bool _sizeDefined;
        ErrorVariableInformation _pendingNodeError;
        IVariableInformation _node;

        protected override string IncludeView => _linkedListItems.IncludeView;
        protected override string ExcludeView => _linkedListItems.ExcludeView;
        protected override string Condition => _linkedListItems.Condition;
        protected override bool Optional => _linkedListItems.Optional;
        protected override string VisualizerName => "<LinkedListItems>";

        LinkedListItemsEntity(IVariableInformation variable, NatvisScope natvisScope,
                              LinkedListItemsType linkedListItems, NatvisDiagnosticLogger logger,
                              NatvisEntityStore store, NatvisExpressionEvaluator evaluator,
                              NatvisSizeParser sizeParser)
            : base(variable, logger, evaluator, natvisScope)
        {
            _linkedListItems = linkedListItems;
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

            await EvaluateUpToAsync(from + count);
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

        protected override async Task ValidateAsync()
        {
            if (string.IsNullOrWhiteSpace(_linkedListItems.HeadPointer))
            {
                throw new InvalidOperationException("No valid <HeadPointer> found.");
            }

            if (string.IsNullOrWhiteSpace(_linkedListItems.NextPointer))
            {
                throw new InvalidOperationException("No valid <NextPointer> found.");
            }

            if (string.IsNullOrWhiteSpace(_linkedListItems.ValueNode?.Value))
            {
                throw new InvalidOperationException("No valid <ValueNode> found.");
            }

            if (_node == null && _lastIndex == 0)
            {
                _node = await GetHeadAsync();
            }
        }

        protected override async Task<int> InitChildrenCountAsync()
        {
            if (_linkedListItems.Size != null && _linkedListItems.Size.Length > 0)
            {
                _sizeDefined = true;
                return (int)await _sizeParser.ParseSizeAsync(_linkedListItems.Size, _variable,
                                                             _natvisScope);
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

        bool IsExpectingMoreChildren()
        {
            if (_pendingNodeError != null)
            {
                return true;
            }

            return _node != null && !_node.IsNullPointer() && _lastIndex < ChildrenLimit;
        }

        async Task<int> EvaluateChildrenCountAsync()
        {
            while (IsExpectingMoreChildren())
            {
                await StoreNodeAndGetNextAsync();
            }

            return _lastIndex;
        }

        async Task EvaluateUpToAsync(int to)
        {
            while (_lastIndex < to)
            {
                await StoreNodeAndGetNextAsync();
            }
        }

        async Task StoreNodeAndGetNextAsync()
        {
            if (_pendingNodeError != null)
            {
                _store.SaveVariable(_lastIndex, _pendingNodeError);
                _pendingNodeError = null;
            }
            else if (_node.IsNullPointer())
            {
                if (_sizeDefined)
                {
                    int size = _store.ChildrenCount;
                    _logger.Warning("<LinkedListItems> declared a size of " +
                                    $"{size} but only {_lastIndex} item(s) found.");

                    _pendingNodeError = new ErrorVariableInformation(
                        "<Error>", $"Size declared as {size} but only {_lastIndex} item(s) found.");
                }
                else
                {
                    _pendingNodeError = new ErrorVariableInformation(
                        "<Error>", $"Item {_lastIndex} is out of bound.");
                }

                _store.SaveVariable(_lastIndex, _pendingNodeError);
            }
            else
            {
                _store.SaveVariable(_lastIndex, await GetDisplayVariableAsync(_lastIndex, _node));
                _node = await GetNextAsync(_node);
            }

            _lastIndex++;
        }

        async Task<IVariableInformation> GetHeadAsync()
        {
            if (_linkedListItems.HeadPointer == "this")
            {
                return _variable;
            }

            return await _evaluator.EvaluateExpressionAsync(_linkedListItems.HeadPointer, _variable,
                                                            _natvisScope,
                                                            _linkedListItems.HeadPointer);
        }

        async Task<IVariableInformation> GetNextAsync(IVariableInformation current)
        {
            try
            {
                return await _evaluator.EvaluateExpressionAsync(_linkedListItems.NextPointer,
                                                                current, _natvisScope, null);
            }
            catch (ExpressionEvaluationFailed ex)
            {
                _pendingNodeError = Optional ? new ErrorVariableInformation("<Warning>", ex.Message)
                                             : new ErrorVariableInformation("<Error>", ex.Message);

                return null;
            }
        }

        async Task<IVariableInformation> GetDisplayVariableAsync(
            int index, IVariableInformation node)
        {
            string elementName = $"[{index}]";
            if (_linkedListItems.ValueNode.Value == "this")
            {
                return new NamedVariableInformation(node, elementName);
            }

            return await _evaluator.GetExpressionValueOrErrorAsync(_linkedListItems.ValueNode.Value,
                                                                   node, _natvisScope, elementName,
                                                                   "LinkedListItems");
        }
    }
}
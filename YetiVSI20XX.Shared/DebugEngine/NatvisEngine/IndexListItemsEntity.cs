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
    // example:
    //    <IndexListItems>
    //      <Size>_numElements</Size>
    //      <ValueNode>_elements[$i]</ValueNode>
    //    </IndexListItems>
    public class IndexListItemsEntity : LeafEntity
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
                                        IndexListItemsType indexListItems) =>
                new IndexListItemsEntity(variable, natvisScope, indexListItems, _logger,
                                         new NatvisEntityStore(), _evaluator, _sizeParser);
        }

        readonly IndexListItemsType _indexListItems;
        readonly NatvisEntityStore _store;
        readonly NatvisSizeParser _sizeParser;

        bool _initialized;

        protected override string IncludeView => _indexListItems.IncludeView;
        protected override string ExcludeView => _indexListItems.ExcludeView;
        protected override string Condition => _indexListItems.Condition;
        protected override bool Optional => _indexListItems.Optional;
        protected override string VisualizerName => "<IndexListItems>";

        IndexListItemsEntity(IVariableInformation variable, NatvisScope natvisScope,
                             IndexListItemsType indexListItems, NatvisDiagnosticLogger logger,
                             NatvisEntityStore store, NatvisExpressionEvaluator evaluator,
                             NatvisSizeParser sizeParser)
            : base(variable, logger, evaluator, natvisScope)
        {
            _indexListItems = indexListItems;
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

            var result = new List<IVariableInformation>();

            if (_store.ValidationError != null)
            {
                result.Add(_store.ValidationError);
                return result.GetRange(from, count);
            }

            var scope = new NatvisScope(_natvisScope);
            for (int index = from; index < from + count; index++)
            {
                IVariableInformation varInfo = await _store.GetOrEvaluateAsync(index, async i => {
                    scope.AddScopedName("$i", $"{i}U");
                    string displayName = $"[{i}]";

                    // From the list of all <ValueNode> children, filter all with non-empty body
                    // and return the first which Condition evaluates to true.
                    IndexNodeType valueNode = await FindFirstValidIndexNodeAsync(scope);
                    if (valueNode == null)
                    {
                        // For the current index $i, there is no <ValueNode> which passes the
                        // Condition check.
                        return new ErrorVariableInformation(
                            displayName, "<Error> No valid <ValueNode> found.");
                    }

                    return await _evaluator.GetExpressionValueOrErrorAsync(
                        valueNode.Value, _variable, scope, displayName, "IndexListItems");
                });

                result.Add(varInfo);
            }

            return result;
        }

        public override async Task<bool> IsValidAsync()
        {
            await InitAsync();
            return _store.ValidationError == null;
        }

        #endregion

        protected override Task ValidateAsync()
        {
            // Check if there is a <ValueNode> with non-empty body.
            IndexNodeType valueNode =
                _indexListItems.ValueNode?.Where(v => !string.IsNullOrWhiteSpace(v.Value))
                    .FirstOrDefault();

            if (valueNode == null)
            {
                throw new InvalidOperationException("No valid <ValueNode> found.");
            }

            return Task.CompletedTask;
        }

        protected override async Task<int> InitChildrenCountAsync() =>
            (int)await _sizeParser.ParseSizeAsync(_indexListItems.Size, _variable, _natvisScope);

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

        async Task<IndexNodeType> FindFirstValidIndexNodeAsync(NatvisScope scope)
        {
            if (_indexListItems.ValueNode == null)
            {
                throw new InvalidOperationException(
                    "<IndexListItems> must have at least one <ValueNode> entry");
            }

            foreach (var vn in _indexListItems.ValueNode)
            {
                if (string.IsNullOrWhiteSpace(vn.Value))
                {
                    _logger.Warning("Found empty <ValueNode> entry");
                    continue;
                }

                // Pick the first entry whose condition evaluates to "true".
                bool condition = await _evaluator.EvaluateConditionAsync(
                    vn.Condition, _variable, scope);
                if (condition)
                {
                    return vn;
                }
            }
            _logger.Warning("No valid <ValueNode> found");
            return null;
        }
    }
}
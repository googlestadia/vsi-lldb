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
using YetiCommon.Util;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.DebugEngine.NatvisEngine
{
    public class ArrayItemsEntity : LeafEntity
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
                                        ArrayItemsType arrayItems) =>
                new ArrayItemsEntity(variable, natvisScope, arrayItems, _logger,
                                     new NatvisEntityStore(), _evaluator, _sizeParser);
        }

        readonly ArrayItemsType _arrayListItems;
        readonly NatvisEntityStore _store;
        readonly NatvisSizeParser _sizeParser;
        bool _initialized;
        ValuePointerType _valuePointer;

        protected override string IncludeView => _arrayListItems.IncludeView;
        protected override string ExcludeView => _arrayListItems.ExcludeView;
        protected override string Condition => _arrayListItems.Condition;
        protected override bool Optional => _arrayListItems.Optional;
        protected override string VisualizerName => "<ArrayItems>";

        ArrayItemsEntity(IVariableInformation variable, NatvisScope natvisScope,
                         ArrayItemsType arrayListItems, NatvisDiagnosticLogger logger,
                         NatvisEntityStore store, NatvisExpressionEvaluator evaluator,
                         NatvisSizeParser sizeParser)
            : base(variable, logger, evaluator, natvisScope)
        {
            _arrayListItems = arrayListItems;
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

            for (int index = from; index < from + count; index++)
            {
                IVariableInformation varInfo = await _store.GetOrEvaluateAsync(
                    index, async i => await _evaluator.GetExpressionValueOrErrorAsync(
                               $"({_valuePointer.Value})[{i}]", _variable, _natvisScope, $"[{i}]",
                               "ArrayItems"));

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

        protected override async Task ValidateAsync()
        {
            Task<ValuePointerType> valuePointerTask =
                _arrayListItems.ValuePointer?.Where(v => !string.IsNullOrWhiteSpace(v.Value))
                    .FirstOrDefaultAsync(async v => await _evaluator.EvaluateConditionAsync(
                                             v.Condition, _variable, _natvisScope));

            if (valuePointerTask != null)
            {
                _valuePointer = await valuePointerTask;
            }

            if (_valuePointer == null)
            {
                throw new InvalidOperationException("No valid <ValuePointer> found.");
            }
        }

        protected override async Task<int> InitChildrenCountAsync() =>
            (int)await _sizeParser.ParseSizeAsync(_arrayListItems.Size, _variable, _natvisScope);

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
    }
}
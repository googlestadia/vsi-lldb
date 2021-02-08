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
using System.Threading.Tasks;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.DebugEngine.NatvisEngine
{
    // example:
    //    <Item Name="[itemName]">itemValue</Item>
    public class ItemEntity : LeafEntity
    {
        public class Factory
        {
            readonly NatvisDiagnosticLogger _logger;
            readonly NatvisExpressionEvaluator _evaluator;

            [Obsolete("This constructor only exists to support mocking libraries.", error: true)]
            public Factory()
            {
            }

            public Factory(NatvisDiagnosticLogger logger, NatvisExpressionEvaluator evaluator)
            {
                _logger = logger;
                _evaluator = evaluator;
            }

            public INatvisEntity Create(IVariableInformation variable, NatvisScope natvisScope,
                                        ItemType item) => new ItemEntity(variable, natvisScope,
                                                                         item, _logger,
                                                                         new NatvisEntityStore(),
                                                                         _evaluator);
        }

        readonly ItemType _item;
        readonly NatvisEntityStore _store;

        bool _initialized;
        protected override string IncludeView => _item.IncludeView;
        protected override string ExcludeView => _item.ExcludeView;
        protected override string Condition => _item.Condition;
        protected override bool Optional => _item.Optional;
        protected override string VisualizerName => "<Item>";

        ItemEntity(IVariableInformation variable, NatvisScope natvisScope, ItemType item,
                   NatvisDiagnosticLogger logger, NatvisEntityStore store,
                   NatvisExpressionEvaluator evaluator)
            : base(variable, logger, evaluator, natvisScope)
        {
            _item = item;
            _store = store;
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

            var result = new List<IVariableInformation>
                {_store.ValidationError ?? await EvaluateItemAsync()};

            return result.GetRange(from, count);
        }

        public override async Task<bool> IsValidAsync()
        {
            await InitAsync();
            return _store.ValidationError == null;
        }

        #endregion

        protected override Task ValidateAsync() => Task.CompletedTask;

        protected override async Task<int> InitChildrenCountAsync()
        {
            await EvaluateItemAsync();
            return 1;
        }

        async Task<IVariableInformation> EvaluateItemAsync() => await _store.GetOrEvaluateAsync(
            0, async i => await _evaluator.EvaluateExpressionAsync(_item.Value, _variable,
                                                                   _natvisScope, _item.Name));

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
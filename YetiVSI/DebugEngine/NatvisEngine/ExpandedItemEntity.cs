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
    // <Type Name="std::auto_ptr&lt;*&gt;">
    //   <DisplayString>auto_ptr {*_Myptr}</DisplayString>
    //   <Expand>
    //     <ExpandedItem>_Myptr</ExpandedItem>
    //   </Expand>
    // </Type>
    public class ExpandedItemEntity : LeafEntity
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
                                        ExpandedItemType expandedItem) =>
                new ExpandedItemEntity(variable, natvisScope, expandedItem, _logger,
                                       new NatvisEntityStore(), _evaluator);
        }

        readonly ExpandedItemType _expandedItem;
        readonly NatvisEntityStore _store;

        IChildAdapter _childAdapter;
        bool _initialized;

        protected override string IncludeView => _expandedItem.IncludeView;
        protected override string ExcludeView => _expandedItem.ExcludeView;
        protected override string Condition => _expandedItem.Condition;
        protected override bool Optional => _expandedItem.Optional;
        protected override string VisualizerName => "<ExpandedItem>";

        ExpandedItemEntity(IVariableInformation variable, NatvisScope natvisScope,
                           ExpandedItemType expandedItem, NatvisDiagnosticLogger logger,
                           NatvisEntityStore store, NatvisExpressionEvaluator evaluator)
            : base(variable, logger, evaluator, natvisScope)
        {
            _expandedItem = expandedItem;
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

            if (await CountChildrenAsync() == 0)
            {
                return new List<IVariableInformation>();
            }

            if (_store.ValidationError != null)
            {
                return new List<IVariableInformation>() {_store.ValidationError}.GetRange(
                    from, count);
            }

            return await _childAdapter.GetChildrenAsync(from, count);
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
            if (string.IsNullOrWhiteSpace(_expandedItem?.Value))
            {
                return 0;
            }

            await InitChildAdapterAsync();
            return await _childAdapter.CountChildrenAsync();
        }

        async Task InitChildAdapterAsync()
        {
            if (_childAdapter == null)
            {
                IVariableInformation expandInfo = await _evaluator.EvaluateExpressionAsync(
                    _expandedItem.Value, _variable, _natvisScope, null);

                _childAdapter = await expandInfo.GetChildAdapterAsync();
            }
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
    }
}
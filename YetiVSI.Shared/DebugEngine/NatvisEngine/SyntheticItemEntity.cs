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
    public class SyntheticItemEntity : LeafEntity
    {
        public class Factory
        {
            readonly NatvisDiagnosticLogger _logger;
            readonly NatvisExpressionEvaluator _evaluator;
            readonly NatvisStringFormatter _stringFormatter;

            [Obsolete("This constructor only exists to support mocking libraries.", error: true)]
            public Factory()
            {
            }

            public Factory(NatvisDiagnosticLogger logger, NatvisExpressionEvaluator evaluator,
                           NatvisStringFormatter stringFormatter)
            {
                _logger = logger;
                _evaluator = evaluator;
                _stringFormatter = stringFormatter;
            }

            public INatvisEntity Create(IVariableInformation variable, NatvisScope natvisScope,
                                        SyntheticItemType item,
                                        NatvisCollectionEntity.Factory natvisCollectionFactory) =>
                new SyntheticItemEntity(variable, natvisScope, item, _logger,
                                        new NatvisEntityStore(), _evaluator, _stringFormatter,
                                        natvisCollectionFactory);
        }

        readonly SyntheticItemType _item;
        readonly NatvisEntityStore _store;
        readonly NatvisStringFormatter _stringFormatter;
        readonly NatvisCollectionEntity.Factory _natvisCollectionFactory;

        bool _initialized;

        protected override string IncludeView => _item.IncludeView;
        protected override string ExcludeView => _item.ExcludeView;
        protected override string Condition => _item.Condition;
        protected override bool Optional => _item.Optional;
        protected override string VisualizerName => "<Synthetic>";

        SyntheticItemEntity(IVariableInformation variable, NatvisScope natvisScope,
                            SyntheticItemType item, NatvisDiagnosticLogger logger,
                            NatvisEntityStore store, NatvisExpressionEvaluator evaluator,
                            NatvisStringFormatter stringFormatter,
                            NatvisCollectionEntity.Factory natvisCollectionFactory)
            : base(variable, logger, evaluator, natvisScope)
        {
            _item = item;
            _store = store;
            _stringFormatter = stringFormatter;
            _natvisCollectionFactory = natvisCollectionFactory;
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

        async Task<IVariableInformation> EvaluateItemAsync()
        {
            if (_store.HasVariable(0))
            {
                return _store.GetVariable(0);
            }

            var formatStringContext = _item.DisplayString == null
                                          ? new NatvisStringFormatter.FormatStringContext()
                                          : new NatvisStringFormatter.FormatStringContext {
                                                StringElements = _item.DisplayString.Select(
                                                    e => new DisplayStringElement(e)),
                                                NatvisScope = _natvisScope
                                            };

            string displayName =
                await _stringFormatter.FormatDisplayStringAsync(formatStringContext, _variable);

            IVariableInformation syntheticInfo =
                new NatvisSyntheticVariableInformation(_stringFormatter, _natvisCollectionFactory,
                                                       _natvisScope, _item, _variable, displayName);

            _store.SaveVariable(0, syntheticInfo);
            return syntheticInfo;
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
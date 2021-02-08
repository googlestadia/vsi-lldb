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
    //       <CustomListItems>
    //         <Variable Name = "index" InitialValue="0" />
    //         <Loop>
    //           <If Condition = "index > 10">
    //             <Break/>
    //           </If>
    //           <Item>arr[index]</Item>
    //           <Exec>index++</ Exec >
    //           </Loop>
    //         </CustomListItems>
    public class CustomListItemsEntity : LeafEntity
    {
        public class Factory
        {
            readonly NatvisDiagnosticLogger _logger;
            readonly NatvisExpressionEvaluator _evaluator;
            readonly IVariableNameTransformer _nameTransformer;

            [Obsolete("This constructor only exists to support mocking libraries.", error: true)]
            public Factory()
            {
            }

            public Factory(NatvisDiagnosticLogger logger, NatvisExpressionEvaluator evaluator,
                           IVariableNameTransformer nameTransformer)
            {
                _logger = logger;
                _evaluator = evaluator;
                _nameTransformer = nameTransformer;
            }

            public INatvisEntity Create(IVariableInformation variable, NatvisScope natvisScope,
                                        CustomListItemsType customListItems) =>
                new CustomListItemsEntity(variable, natvisScope, customListItems, _logger,
                                          new NatvisEntityStore(), _evaluator, _nameTransformer,
                                          new CodeBlockParser(_evaluator));
        }

        readonly CustomListItemsType _customList;
        readonly NatvisEntityStore _store;
        readonly IVariableNameTransformer _nameTransformer;
        readonly CodeBlockParser _parser;

        bool _initialized;
        bool _hasEvaluationError;

        int _lastIndex;
        ICodeBlock _blocks;

        CustomListItemsContext _ctx;

        protected override string IncludeView => _customList.IncludeView;
        protected override string ExcludeView => _customList.ExcludeView;
        protected override string Condition => _customList.Condition;
        protected override bool Optional => _customList.Optional;
        protected override string VisualizerName => "<CustomListItems>";

        CustomListItemsEntity(IVariableInformation variable, NatvisScope natvisScope,
                              CustomListItemsType customList, NatvisDiagnosticLogger logger,
                              NatvisEntityStore store, NatvisExpressionEvaluator evaluator,
                              IVariableNameTransformer nameTransformer, CodeBlockParser parser)
            : base(variable, logger, evaluator, natvisScope)
        {
            _customList = customList;
            _store = store;
            _nameTransformer = nameTransformer;
            _parser = parser;
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
            return Optional || (_store.ValidationError == null && !_hasEvaluationError);
        }

        #endregion

        protected override void OnChildrenLimitIncreased()
        {
            _initialized = false;
        }

        protected override Task ValidateAsync() => Task.CompletedTask;

        protected override async Task<int> InitChildrenCountAsync()
        {
            await EvaluateUpToAsync(ChildrenLimit);
            return _lastIndex;
        }

        protected async Task EvaluateUpToAsync(int to)
        {
            if (_store.ValidationError != null || _hasEvaluationError)
            {
                return;
            }

            try
            {
                await InitContextAsync();

                while (_lastIndex < to && _blocks.State != BlockState.Done)
                {
                    EvaluateResult result = await _blocks.EvaluateAsync();

                    if (result.Type == ResultType.Var)
                    {
                        _store.SaveVariable(_lastIndex, result.Data);
                        _lastIndex++;
                    }
                }
            }
            catch (ExpressionEvaluationFailed ex)
            {
                ErrorVariableInformation error =
                    NatvisErrorUtils.LogAndGetExpandChildrenValidationError(
                        Optional ? NatvisLoggingLevel.WARNING : NatvisLoggingLevel.ERROR, _logger,
                        VisualizerName, _variable?.TypeName, ex.Message);

                _hasEvaluationError = true;

                if (_lastIndex != 0 || !Optional)
                {
                    _store.SaveVariable(_lastIndex, error);
                    _lastIndex++;
                }
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

        async Task InitContextAsync()
        {
            if (_ctx != null && _blocks != null)
            {
                return;
            }

            _ctx = new CustomListItemsContext(new NatvisScope(_natvisScope), _variable);

            foreach (var varType in _customList.Variable ?? Enumerable.Empty<VariableType>())
            {
                _ctx.NatvisScope.SetScopedName(varType.Name,
                                               _nameTransformer.TransformName(varType.Name));
                await _evaluator.DeclareVariableAsync(_variable, varType.Name, varType.InitialValue,
                                                      _ctx.NatvisScope);
            }

            _blocks = new MultipleInstructionsBlock(
                _parser.Parse(_customList.CodeBlock ?? new object[0], _ctx));
        }
    }
}
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
    //         </Loop>
    //       </CustomListItems>
    public class CustomListItemsEntity : LeafEntity
    {
        public class Factory
        {
            readonly NatvisDiagnosticLogger _logger;
            readonly NatvisExpressionEvaluator _evaluator;
            readonly IVariableNameTransformer _nameTransformer;
            readonly NatvisSizeParser _sizeParser;

            [Obsolete("This constructor only exists to support mocking libraries.", error: true)]
            public Factory()
            {
            }

            public Factory(NatvisDiagnosticLogger logger, NatvisExpressionEvaluator evaluator,
                           IVariableNameTransformer nameTransformer, NatvisSizeParser sizeParser)
            {
                _logger = logger;
                _evaluator = evaluator;
                _nameTransformer = nameTransformer;
                _sizeParser = sizeParser;
            }

            public INatvisEntity Create(IVariableInformation variable, NatvisScope natvisScope,
                                        CustomListItemsType customListItems) =>
                new CustomListItemsEntity(variable, natvisScope, customListItems, _logger,
                                          new NatvisEntityStore(), _evaluator, _nameTransformer,
                                          new CodeBlockParser(_evaluator), _sizeParser);
        }

        readonly CustomListItemsType _customList;
        readonly NatvisEntityStore _store;
        readonly IVariableNameTransformer _nameTransformer;
        readonly NatvisSizeParser _sizeParser;
        readonly CodeBlockParser _parser;

        bool _initialized;
        bool _hasEvaluationError;

        int _lastIndex;
        ICodeBlock _blocks;
        ErrorVariableInformation _emptyErrorNode;

        CustomListItemsContext _ctx;

        protected override string IncludeView => _customList.IncludeView;
        protected override string ExcludeView => _customList.ExcludeView;
        protected override string Condition => _customList.Condition;
        protected override bool Optional => _customList.Optional;
        protected override string VisualizerName => "<CustomListItems>";

        CustomListItemsEntity(IVariableInformation variable, NatvisScope natvisScope,
                              CustomListItemsType customList, NatvisDiagnosticLogger logger,
                              NatvisEntityStore store, NatvisExpressionEvaluator evaluator,
                              IVariableNameTransformer nameTransformer, CodeBlockParser parser,
                              NatvisSizeParser sizeParser)
            : base(variable, logger, evaluator, natvisScope)
        {
            _customList = customList;
            _store = store;
            _nameTransformer = nameTransformer;
            _parser = parser;
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
            await InitContextAsync();

            if (_ctx.Size != null)
            {
                return (int)_ctx.Size;
            }

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

                // If execution of <CustomListItems> reached end before reaching the specified
                // <Size> limit, fill the remaining children with error nodes.
                if (_blocks.State == BlockState.Done && _ctx.Size != null)
                {
                    while (_lastIndex < to)
                    {
                        if (_emptyErrorNode == null)
                        {
                            _logger.Warning("<CustomListItems> declared a size of " +
                                            $"{_ctx.Size} but only {_lastIndex} item(s) found.");

                            _emptyErrorNode = new ErrorVariableInformation(
                                "<Error>",
                                $"Size declared as {_ctx.Size} but only {_lastIndex} item(s) found.");
                        }

                        _store.SaveVariable(_lastIndex, _emptyErrorNode);
                        _lastIndex++;
                    }
                }
            }
            catch (ExpressionEvaluationFailed ex)
            {
                HandleExpressionEvaluationError(ex);
            }
        }
        private void HandleExpressionEvaluationError(ExpressionEvaluationFailed ex)
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

            // Handle <Variable> children.
            try
            {
                foreach (var varType in _customList.Variable ?? Enumerable.Empty<VariableType>())
                {
                    _ctx.NatvisScope.AddScopedName(varType.Name,
                                                   _nameTransformer.TransformName(varType.Name));

                    await _evaluator.DeclareVariableAsync(_variable, varType.Name,
                                                          varType.InitialValue, _ctx.NatvisScope);
                }
            }
            catch (ExpressionEvaluationFailed ex)
            {
                HandleExpressionEvaluationError(ex);
            }

            // Handle <Size> children.
            try
            {
                _ctx.Size =
                    await _sizeParser.ParseSizeAsync(_customList.Size, _variable, _ctx.NatvisScope);
            }
            catch (InvalidOperationException)
            {
                // InvalidOperationException is thrown if there's no valid <Size> attribute.
                // Since <Size> is optional in <CustomListItems>, ignore this exception.
            }
            catch (ExpressionEvaluationFailed ex)
            {
                HandleExpressionEvaluationError(ex);
            }

            _blocks = new MultipleInstructionsBlock(
                _parser.Parse(_customList.CodeBlock ?? new object[0], _ctx));
        }
    }
}
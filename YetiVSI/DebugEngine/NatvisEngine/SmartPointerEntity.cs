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
    // <Type Name="SmartPointerExample">
    //   <SmartPointer Usage="Minimal">ptr</SmartPointer>
    // </Type>
    public class SmartPointerEntity : INatvisEntity
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

            public SmartPointerEntity Create(IVariableInformation variable,
                                             SmartPointerType smartPointerItem,
                                             IDictionary<string, string> scopedNames,
                                             IChildAdapter fallbackAdapter) =>
                new SmartPointerEntity(_evaluator, _logger, variable, smartPointerItem, scopedNames,
                                       fallbackAdapter);
        }

        readonly NatvisExpressionEvaluator _evaluator;
        readonly NatvisDiagnosticLogger _logger;
        readonly IVariableInformation _variable;
        readonly SmartPointerType _smartPointerItem;
        readonly IDictionary<string, string> _scopedNames;
        readonly IChildAdapter _fallbackAdapter;

        IChildAdapter _adapter;

        bool _initialized;

        SmartPointerEntity(NatvisExpressionEvaluator evaluator, NatvisDiagnosticLogger logger,
                           IVariableInformation variable, SmartPointerType smartPointerItem,
                           IDictionary<string, string> scopedNames, IChildAdapter fallbackAdapter)
        {
            _evaluator = evaluator;
            _logger = logger;
            _variable = variable;
            _smartPointerItem = smartPointerItem;
            _scopedNames = scopedNames;
            _fallbackAdapter = fallbackAdapter;
        }

        public async Task<int> CountChildrenAsync()
        {
            await InitAsync();
            return await _adapter.CountChildrenAsync();
        }

        public async Task<IList<IVariableInformation>> GetChildrenAsync(int from, int count)
        {
            await InitAsync();
            return await _adapter.GetChildrenAsync(from, count);
        }

        public Task<bool> IsValidAsync() => Task.FromResult(true);

        async Task InitAsync()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            if (string.IsNullOrWhiteSpace(_smartPointerItem?.Value) ||
                !NatvisViewsUtil.IsViewVisible(_variable.FormatSpecifier,
                                               _smartPointerItem.IncludeView,
                                               _smartPointerItem.ExcludeView))
            {
                _adapter = _fallbackAdapter;
                return;
            }

            try
            {
                IVariableInformation expandInfo = await _evaluator.EvaluateExpressionAsync(
                    _smartPointerItem.Value, _variable, _scopedNames, null);

                _adapter = expandInfo.GetChildAdapter();
            }
            catch (ExpressionEvaluationFailed ex)
            {
                NatvisErrorUtils.LogExpandChildrenValidationError(
                    NatvisLoggingLevel.WARNING, _logger, "<SmartPointer>", _variable.TypeName,
                    ex.Message);

                _adapter = _fallbackAdapter;
            }
        }
    }
}
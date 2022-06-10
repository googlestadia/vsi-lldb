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
    class UnsupportedNatvisEntity : INatvisEntity
    {
        readonly IVariableInformation _variable;
        readonly Type _entityType;
        readonly NatvisDiagnosticLogger _logger;

        public UnsupportedNatvisEntity(IVariableInformation variable, Type entityType,
                                       NatvisDiagnosticLogger logger)
        {
            _variable = variable;
            _entityType = entityType;
            _logger = logger;
        }

        public Task<int> CountChildrenAsync() => Task.FromResult(1);

        public Task<IList<IVariableInformation>> GetChildrenAsync(int from, int count)
        {
            ErrorVariableInformation errInfo =
                NatvisErrorUtils.LogAndGetExpandChildrenValidationError(
                    NatvisLoggingLevel.WARNING, _logger, _entityType.ToString(), _variable.TypeName,
                    $"Encountered unsupported tag: {_entityType}.");

            return Task.FromResult<IList<IVariableInformation>>(new List<IVariableInformation>
                                                                    {errInfo});
        }

        public Task<bool> IsValidAsync() => Task.FromResult(false);
    }
}
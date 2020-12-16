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
    /// <summary>
    /// This class represent a leaf Natvis entity with natvis attributes (e.g. IncludeView,
    /// Optional). It is aimed to standardize initialization logic across all the elements which
    /// includes:
    /// - Checking whether the elements should be expanded
    /// - Getting the number of elements
    /// - Validating that the required nodes are present
    /// - Processing corresponding errors
    /// </summary>
    public abstract class LeafEntity : INatvisEntity, IHasChildrenLimit
    {
        protected class EntityInfo
        {
            public int ChildrenCount { get; }

            public ErrorVariableInformation Error { get; }

            public static EntityInfo Empty() => new EntityInfo(0, null);

            public static EntityInfo WithCount(int count) => new EntityInfo(count, null);

            public static EntityInfo WithError(ErrorVariableInformation error) =>
                new EntityInfo(0, error);

            EntityInfo(int childrenCount, ErrorVariableInformation error)
            {
                ChildrenCount = childrenCount;
                Error = error;
            }
        }

        protected readonly IVariableInformation _variable;
        protected readonly NatvisDiagnosticLogger _logger;
        protected readonly NatvisExpressionEvaluator _evaluator;
        protected readonly IDictionary<string, string> _scopedNames;

        protected LeafEntity(IVariableInformation variable, NatvisDiagnosticLogger logger,
                             NatvisExpressionEvaluator evaluator,
                             IDictionary<string, string> scopedNames)
        {
            _variable = variable;
            _logger = logger;
            _evaluator = evaluator;
            _scopedNames = scopedNames;
        }

        protected abstract string IncludeView { get; }
        protected abstract string ExcludeView { get; }
        protected abstract string Condition { get; }
        protected abstract bool Optional { get; }
        protected abstract string VisualizerName { get; }

        int _childrenLimit;

        public int ChildrenLimit
        {
            get => _childrenLimit;
            set
            {
                if (_childrenLimit < value)
                {
                    _childrenLimit = value;
                    OnChildrenLimitIncreased();
                }
                else
                {
                    _childrenLimit = value;
                }
            }
        }

        public abstract Task<int> CountChildrenAsync();

        public abstract Task<IList<IVariableInformation>> GetChildrenAsync(int from, int count);

        public abstract Task<bool> IsValidAsync();

        /// <summary>
        /// This method is called if the children limit is increased.
        /// Classes which extend LeafChildren and want to make use of ChildrenLimit must override
        /// this method.
        /// </summary>
        protected virtual void OnChildrenLimitIncreased()
        {
        }

        /// <summary>
        /// The implementation should verify that all the required nodes / attributes are declared
        /// for the entity and throw InvalidOperationException if something is missing.
        /// </summary>
        protected abstract Task ValidateAsync();

        /// <summary>
        /// The implementation should return the size of the entity, throw InvalidOperationException
        /// if it misses the information to evaluate the size or throw ExpressionEvaluationFailed
        /// if it is impossible to evaluate a size due to evaluation error.
        /// Note that due to performance optimizations size can change for some elements based
        /// on max children configuration. The implementation must insure that the size always
        /// grows, e.g. if we encounter evaluation exception when requested to update size, we must
        /// assume that all the previous elements are valid.
        /// </summary>
        protected abstract Task<int> InitChildrenCountAsync();

        /// <summary>
        /// This method must be invoked by the entities implementing LeafEntity before accessing
        /// elements or their count. It evaluates the children count and validates the entity
        /// based on InitChildrenCount() and Validate() implementation and processes the errors.
        /// This method is not invoked from constructor to avoid extra calculations if VS never
        /// requests additional information.
        /// </summary>
        /// <returns>Task wrapping Entity info with ChildrenCount and Error information.
        /// ChildrenCount is 0 if element should not be expanded or if error occurred,
        /// otherwise it contains result of InitChildrenCount().
        /// Error contains error occured on InitChildrenCount() or Validate(), otherwise
        /// null.</returns>
        protected async Task<EntityInfo> InitLeafAsync()
        {
            try
            {
                if (!await ShouldBeExpandedAsync())
                {
                    return EntityInfo.Empty();
                }

                await ValidateAsync();
                return EntityInfo.WithCount(await InitChildrenCountAsync());
            }
            catch (Exception ex) when (ex is ExpressionEvaluationFailed ||
                                       ex is InvalidOperationException ||
                                       ex is NotSupportedException)
            {
                if (!Optional)
                {
                    ErrorVariableInformation error =
                        NatvisErrorUtils.LogAndGetExpandChildrenValidationError(
                            NatvisLoggingLevel.ERROR, _logger, VisualizerName, _variable?.TypeName,
                            ex.Message);

                    return EntityInfo.WithError(error);
                }

                NatvisErrorUtils.LogExpandChildrenValidationError(
                    NatvisLoggingLevel.VERBOSE, _logger, VisualizerName, _variable?.TypeName,
                    ex.Message);
                return EntityInfo.Empty();
            }
        }

        async Task<bool> ShouldBeExpandedAsync() =>
            NatvisViewsUtil.IsViewVisible(_variable.FormatSpecifier, IncludeView, ExcludeView) &&
            await _evaluator.EvaluateConditionAsync(Condition, _variable, _scopedNames);
    }
}
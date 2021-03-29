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

using System.Collections.Generic;
using System.Linq;
using YetiVSI.Shared.Metrics;

namespace YetiVSI.Metrics
{
    public class ExpressionEvaluationBatch : IEventBatch<ExpressionEvaluationBatchParams,
        ExpressionEvaluationBatchSummary>
    {
        readonly VSIDebugExpressionEvaluationBatch _protoBatch =
            new VSIDebugExpressionEvaluationBatch();

        public void Add(ExpressionEvaluationBatchParams batchParams)
        {
            var expressionEvaluation =
                new VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation
                {
                    Strategy =
                        (VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types.
                            Strategy) batchParams.StrategyParam,
                    Context =
                        (VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types.
                            Context) batchParams.ContextParam,
                    StartTimestampMicroseconds = batchParams.StartTimestampUs,
                    EndTimestampMicroseconds = batchParams.EndTimestampUs,
                    NatvisValueId = batchParams.NatvisValueId
                };

            expressionEvaluation.EvaluationSteps.AddRange(
                batchParams.EvaluationSteps.Select(CreateExpressionEvaluationStep));

            _protoBatch.ExpressionEvaluations.Add(expressionEvaluation);
        }

        public ExpressionEvaluationBatchSummary GetSummary() =>
            new ExpressionEvaluationBatchSummary(_protoBatch);

        static VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types.
            ExpressionEvaluationStep CreateExpressionEvaluationStep(
                ExpressionEvaluationStep evaluationStep)
        {
            return new
                VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types.
                ExpressionEvaluationStep
                {
                    Engine =
                        (VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types.
                            ExpressionEvaluationStep.Types.Engine) evaluationStep.StepEngine,
                    Result =
                        (VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types.
                            ExpressionEvaluationStep.Types.EngineResult) evaluationStep
                            .StepEngineResult,
                    DurationMicroseconds = evaluationStep.DurationUs
                };
        }
    }

    public class ExpressionEvaluationBatchParams
    {
        public Strategy StrategyParam { get; }
        public Context ContextParam { get; }
        public List<ExpressionEvaluationStep> EvaluationSteps { get; }
        public long StartTimestampUs { get; }
        public long EndTimestampUs { get; }
        public string NatvisValueId { get; }

        public ExpressionEvaluationBatchParams(Strategy strategy, Context context,
                                               List<ExpressionEvaluationStep> evaluationSteps,
                                               long startTimestampUs, long endTimestampUs,
                                               string natvisValueId)
        {
            StrategyParam = strategy;
            ContextParam = context;
            EvaluationSteps = evaluationSteps;
            StartTimestampUs = startTimestampUs;
            EndTimestampUs = endTimestampUs;
            NatvisValueId = natvisValueId;
        }

        public enum Strategy
        {
            UnknownStrategy,
            Lldb,
            LldbEval,
            LldbEvalWithFallback
        }

        public enum Context
        {
            UnknownContext,
            Frame,
            Value
        }
    }

    public class ExpressionEvaluationStep
    {
        public Engine StepEngine { get; }
        public EngineResult StepEngineResult { get; }
        public long DurationUs { get; }

        public ExpressionEvaluationStep(Engine stepEngine, EngineResult stepEngineResult,
                                        long durationUs)
        {
            StepEngine = stepEngine;
            StepEngineResult = stepEngineResult;
            DurationUs = durationUs;
        }

        public enum Engine
        {
            UnknownEngine,
            Lldb,
            LldbVariablePath,
            LldbEval
        }

        public enum EngineResult
        {
            UnknownResult,
            LldbOk,
            LldbError,
            LldbEvalUnknown,
            LldbEvalOk,
            LldbEvalInvalidExpressionSyntax,
            LldbEvalInvalidNumericLiteral,
            LldbEvalInvalidOperandType,
            LldbEvalUndeclaredIdentifier,
            LldbEvalNotImplemented
        }
    }

    public class ExpressionEvaluationBatchSummary
    {
        public VSIDebugExpressionEvaluationBatch Proto { get; }

        public ExpressionEvaluationBatchSummary(VSIDebugExpressionEvaluationBatch proto)
        {
            Proto = proto;
        }
    }
}
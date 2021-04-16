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

using System;
using System.Collections.Generic;
using System.Linq;
using DebuggerApi;
using YetiVSI.DebugEngine;
using YetiVSI.Shared.Metrics;

namespace YetiVSI.Metrics
{
    public class ExpressionEvaluationBatch : IEventBatch<ExpressionEvaluationBatchParams,
        ExpressionEvaluationBatchSummary>
    {
        readonly List<ExpressionEvaluationBatchParams> _batch =
            new List<ExpressionEvaluationBatchParams>();

        public void Add(ExpressionEvaluationBatchParams batchParams)
        {
            _batch.Add(batchParams);
        }

        public ExpressionEvaluationBatchSummary GetSummary()
        {
            var batchProto = new VSIDebugExpressionEvaluationBatch();
            batchProto.ExpressionEvaluations.AddRange(
                _batch.Select(evaluation => evaluation.ConvertToProto()));

            return new ExpressionEvaluationBatchSummary(batchProto);
        }
    }

    public class ExpressionEvaluationBatchParams
    {
        public ExpressionEvaluationStrategy StrategyParam { get; }
        public ExpressionEvaluationContext ContextParam { get; }
        public List<ExpressionEvaluationStepBatchParams> EvaluationSteps { get; }
        public long StartTimestampUs { get; }
        public long EndTimestampUs { get; }
        public string NatvisValueId { get; }

        public ExpressionEvaluationBatchParams(ExpressionEvaluationStrategy strategy,
                                               ExpressionEvaluationContext context,
                                               List<ExpressionEvaluationStepBatchParams> evaluationSteps,
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

        internal VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation ConvertToProto()
        {
            return new VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation
            {
                Strategy = GetStrategyProto(),
                Context = GetContextProto(),
                EvaluationSteps =
                    new List<VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types.
                        ExpressionEvaluationStep>(
                        EvaluationSteps.Select(step => step.ConvertToProto())),
                StartTimestampMicroseconds = StartTimestampUs,
                EndTimestampMicroseconds = EndTimestampUs,
                NatvisValueId = NatvisValueId,
            };
        }

        VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types.Strategy
            GetStrategyProto()
        {
            switch (StrategyParam)
            {
                case ExpressionEvaluationStrategy.LLDB:
                    return VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types
                        .Strategy.Lldb;
                case ExpressionEvaluationStrategy.LLDB_EVAL:
                    return VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types
                        .Strategy.LldbEval;
                case ExpressionEvaluationStrategy.LLDB_EVAL_WITH_FALLBACK:
                    return VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types
                        .Strategy.LldbEvalWithFallback;
                default:
                    throw new ArgumentException(
                        "Expression evaluation Strategy value doesn't have correspondent mapping.");
            }
        }

        VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types.Context GetContextProto()
        {
            switch (ContextParam)
            {
                case ExpressionEvaluationContext.FRAME:
                    return VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types
                        .Context.Frame;
                case ExpressionEvaluationContext.VALUE:
                    return VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types
                        .Context.Value;
                default:
                    throw new ArgumentException(
                        "Expression evaluation Context value doesn't have correspondent mapping.");
            }
        }
    }

    public class ExpressionEvaluationStepBatchParams
    {
        public ExpressionEvaluationEngine StepEngine { get; }
        public LLDBErrorCode? LldbErrorCode { get; }
        public LldbEvalErrorCode? LldbEvalErrorCode { get; }
        public long DurationUs { get; }

        public ExpressionEvaluationStepBatchParams(ExpressionEvaluationEngine stepEngine,
                                        LLDBErrorCode lldbErrorCode, long durationUs)
        {
            StepEngine = stepEngine;
            LldbErrorCode = lldbErrorCode;
            DurationUs = durationUs;
        }

        public ExpressionEvaluationStepBatchParams(ExpressionEvaluationEngine stepEngine,
                                        LldbEvalErrorCode lldbEvalErrorCode, long durationUs)
        {
            StepEngine = stepEngine;
            LldbEvalErrorCode = lldbEvalErrorCode;
            DurationUs = durationUs;
        }

        internal VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types.
            ExpressionEvaluationStep ConvertToProto()
        {
            return new
                VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types.
                ExpressionEvaluationStep
                {
                    Engine = GetEngineProto(),
                    Result = GetEngineResultProto(),
                    DurationMicroseconds = DurationUs,
                };
        }

        VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types.ExpressionEvaluationStep.
            Types.Engine GetEngineProto()
        {
            switch (StepEngine)
            {
                case ExpressionEvaluationEngine.LLDB:
                    return VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types
                        .ExpressionEvaluationStep.Types.Engine.Lldb;
                case ExpressionEvaluationEngine.LLDB_VARIABLE_PATH:
                    return VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types
                        .ExpressionEvaluationStep.Types.Engine.LldbVariablePath;
                case ExpressionEvaluationEngine.LLDB_EVAL:
                    return VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types
                        .ExpressionEvaluationStep.Types.Engine.LldbEval;
                default:
                    throw new ArgumentException(
                        "Expression evaluation Engine value doesn't have correspondent mapping.");
            }
        }

        VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types.ExpressionEvaluationStep.
            Types.EngineResult GetEngineResultProto()
        {
            if (LldbErrorCode.HasValue)
            {
                switch (LldbErrorCode)
                {
                    case LLDBErrorCode.OK:
                        return VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types
                            .ExpressionEvaluationStep.Types.EngineResult.LldbOk;
                    case LLDBErrorCode.ERROR:
                        return VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types
                            .ExpressionEvaluationStep.Types.EngineResult.LldbError;
                }
            }

            if (LldbEvalErrorCode.HasValue)
            {
                switch (LldbEvalErrorCode)
                {
                    case DebuggerApi.LldbEvalErrorCode.Unknown:
                        return VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types
                            .ExpressionEvaluationStep.Types.EngineResult.LldbEvalUnknown;
                    case DebuggerApi.LldbEvalErrorCode.Ok:
                        return VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types
                            .ExpressionEvaluationStep.Types.EngineResult.LldbEvalOk;
                    case DebuggerApi.LldbEvalErrorCode.InvalidExpressionSyntax:
                        return VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types
                            .ExpressionEvaluationStep.Types.EngineResult
                            .LldbEvalInvalidExpressionSyntax;
                    case DebuggerApi.LldbEvalErrorCode.InvalidNumericLiteral:
                        return VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types
                            .ExpressionEvaluationStep.Types.EngineResult
                            .LldbEvalInvalidNumericLiteral;
                    case DebuggerApi.LldbEvalErrorCode.InvalidOperandType:
                        return VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types
                            .ExpressionEvaluationStep.Types.EngineResult.LldbEvalInvalidOperandType;
                    case DebuggerApi.LldbEvalErrorCode.UndeclaredIdentifier:
                        return VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types
                            .ExpressionEvaluationStep.Types.EngineResult
                            .LldbEvalUndeclaredIdentifier;
                    case DebuggerApi.LldbEvalErrorCode.NotImplemented:
                        return VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types
                            .ExpressionEvaluationStep.Types.EngineResult.LldbEvalNotImplemented;

                    default:
                        // TODO: Currently error code from lldb-eval can be -1 (and
                        // potentially other values too), map them to "Unknown". This should
                        // be fixed on lldb-eval side.
                        return VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types
                            .ExpressionEvaluationStep.Types.EngineResult.LldbEvalUnknown;
                }
            }

            throw new ArgumentException(
                "Expression evaluation EngineResult value doesn't have correspondent mapping.");
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
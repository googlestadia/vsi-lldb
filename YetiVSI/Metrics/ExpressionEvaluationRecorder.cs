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
using DebuggerApi;
using YetiVSI.DebugEngine;
using YetiVSI.Shared.Metrics;

namespace YetiVSI.Metrics
{
    public class ExpressionEvaluationRecorder
    {
        readonly IBatchEventAggregator<ExpressionEvaluationBatchParams,
            ExpressionEvaluationBatchSummary> _batchEventAggregator;

        readonly IMetrics _metrics;

        public ExpressionEvaluationRecorder(
            IBatchEventAggregator<ExpressionEvaluationBatchParams, ExpressionEvaluationBatchSummary>
                batchEventAggregator, IMetrics metrics)
        {
            _batchEventAggregator = batchEventAggregator;
            _metrics = metrics;

            _batchEventAggregator.BatchSummaryReady +=
                (_, batchSummary) => OnBatchSummaryReady(batchSummary);
        }

        public void Record(ExpressionEvaluationStrategy expressionEvaluationStrategy,
                           ExpressionEvaluationContext expressionEvaluationContext,
                           StepsRecorder stepsRecorder, long startTimestampUs, long endTimestampUs)
        {
            // Expression evaluation with 'Value' context should use the method signature that
            // includes the natvisValueId parameter. This method is only meant to be used by
            // 'Frame' context expression evaluations.
            if (expressionEvaluationContext == ExpressionEvaluationContext.VALUE)
            {
                throw new ArgumentException(
                    "Record method invocation with 'Value' context should include natvisValueId.");
            }

            Record(expressionEvaluationStrategy, expressionEvaluationContext, stepsRecorder,
                   startTimestampUs, endTimestampUs, null);
        }

        public void Record(ExpressionEvaluationStrategy strategy,
                           ExpressionEvaluationContext context, StepsRecorder stepsRecorder,
                           long startTimestampUs, long endTimestampUs, string natvisValueId)
        {
            var batchParams = new ExpressionEvaluationBatchParams(
                strategy, context, stepsRecorder.GetStepsList(), startTimestampUs, endTimestampUs,
                natvisValueId);
            _batchEventAggregator.Add(batchParams);
        }

        void OnBatchSummaryReady(ExpressionEvaluationBatchSummary batchSummary)
        {
            var logEvent = new DeveloperLogEvent
            {
                DebugExpressionEvaluationBatch = batchSummary.Proto,
                StatusCode = DeveloperEventStatus.Types.Code.Success
            };

            _metrics.RecordEvent(DeveloperEventType.Types.Type.VsiDebugExpressionEvaluationBatch,
                                 logEvent);
        }

        public class StepsRecorder
        {
            readonly List<ExpressionEvaluationStepBatchParams> _steps;

            public StepsRecorder()
            {
                _steps = new List<ExpressionEvaluationStepBatchParams>();
            }

            protected internal List<ExpressionEvaluationStepBatchParams> GetStepsList() => _steps;

            // Add step for LLDB and LLDB_VARIABLE_PATH engines.
            public void Add(ExpressionEvaluationEngine engine, LLDBErrorCode errorCode,
                            long durationUs)
            {
                if (engine != ExpressionEvaluationEngine.LLDB &&
                    engine != ExpressionEvaluationEngine.LLDB_VARIABLE_PATH)
                {
                    throw new ArgumentException(
                        $"Engine {engine} is incompatible with LLDBErrorCode ({errorCode}). "+
                        "Check the parameters for the invoked method.");
                }

                _steps.Add(new ExpressionEvaluationStepBatchParams(engine, errorCode, durationUs));
            }

            // Add step for LLDB_EVAL engine.
            public void Add(ExpressionEvaluationEngine engine, LldbEvalErrorCode lldbEvalErrorCode,
                            long durationUs)
            {
                if (engine != ExpressionEvaluationEngine.LLDB_EVAL)
                {
                    throw new ArgumentException(
                        $"Engine {engine} is incompatible with LldbEvalErrorCode" +
                        $" ({lldbEvalErrorCode}). Check the parameters for the invoked method.");
                }

                _steps.Add(
                    new ExpressionEvaluationStepBatchParams(engine, lldbEvalErrorCode, durationUs));
            }
        }
    }
}
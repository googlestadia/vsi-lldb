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
using Metrics.Shared;
using YetiCommon.PerformanceTracing;
using YetiVSI.DebugEngine;

namespace YetiVSI.Metrics
{
    public class ExpressionEvaluationRecorder
    {
        readonly IBatchEventAggregator<ExpressionEvaluationBatchParams,
            ExpressionEvaluationBatchSummary> _batchEventAggregator;

        readonly IVsiMetrics _metrics;

        public ExpressionEvaluationRecorder(
            IBatchEventAggregator<ExpressionEvaluationBatchParams, ExpressionEvaluationBatchSummary>
                batchEventAggregator, IVsiMetrics metrics)
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

        public void Flush() => _batchEventAggregator.Flush();

        public class StepsRecorder
        {
            readonly List<ExpressionEvaluationStepBatchParams> _steps;
            readonly ITimeSource _timeSource;

            public StepsRecorder(ITimeSource timeSource)
            {
                _steps = new List<ExpressionEvaluationStepBatchParams>();
                _timeSource = timeSource;
            }

            protected internal List<ExpressionEvaluationStepBatchParams> GetStepsList() => _steps;

            public Step NewStep(ExpressionEvaluationEngine engine) =>
                new Step(this, _timeSource, engine);

            protected internal void AddStep(ExpressionEvaluationStepBatchParams stepParams) =>
                _steps.Add(stepParams);

            public class Step : IDisposable
            {
                readonly StepsRecorder _stepsRecorder;
                readonly ITimeSource _timeSource;
                readonly ExpressionEvaluationEngine _engine;
                readonly long _startTicks;

                bool _finalized;

                internal Step(StepsRecorder stepsRecorder, ITimeSource timeSource,
                              ExpressionEvaluationEngine engine)
                {
                    _startTicks = timeSource.GetTimestampTicks();
                    _stepsRecorder = stepsRecorder;
                    _timeSource = timeSource;
                    _engine = engine;
                    _finalized = false;
                }

                // Step finalization for LLDB and LLDB_VARIABLE_PATH engines.
                public void Finalize(LLDBErrorCode lldbErrorCode)
                {
                    long endTicks = _timeSource.GetTimestampTicks();

                    if (_engine != ExpressionEvaluationEngine.LLDB && _engine !=
                        ExpressionEvaluationEngine.LLDB_VARIABLE_PATH)
                    {
                        throw new ArgumentException(
                            $"Engine {_engine} is incompatible with LLDBErrorCode " +
                            $"({lldbErrorCode}). Check the parameters for the invoked method.");
                    }

                    if (_finalized)
                    {
                        throw new InvalidOperationException("Object is already finalized");
                    }

                    var batchParams = new ExpressionEvaluationStepBatchParams(
                        _engine, lldbErrorCode, _timeSource.GetDurationUs(_startTicks, endTicks));
                    _stepsRecorder.AddStep(batchParams);

                    _finalized = true;
                }

                // Step finalization for LLDB_EVAL engine.
                public void Finalize(LldbEvalErrorCode lldbEvalErrorCode)
                {
                    long endTicks = _timeSource.GetTimestampTicks();

                    if (_engine != ExpressionEvaluationEngine.LLDB_EVAL)
                    {
                        throw new ArgumentException(
                            $"Engine {_engine} is incompatible with LldbEvalErrorCode (" +
                            $"{lldbEvalErrorCode}). Check the parameters for the invoked method.");
                    }

                    if (_finalized)
                    {
                        throw new InvalidOperationException("Object is already finalized");
                    }

                    var batchParams = new ExpressionEvaluationStepBatchParams(
                        _engine, lldbEvalErrorCode,
                        _timeSource.GetDurationUs(_startTicks, endTicks));
                    _stepsRecorder.AddStep(batchParams);

                    _finalized = true;
                }

                public void Dispose()
                {
                    if (!_finalized)
                    {
                        throw new InvalidOperationException(
                            "Finalize() must be called before disposing the object");
                    }
                }
            }
        }
    }
}
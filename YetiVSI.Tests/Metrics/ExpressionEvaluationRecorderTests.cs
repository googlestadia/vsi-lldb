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
using DebuggerApi;
using NSubstitute;
using NUnit.Framework;
using YetiCommon.PerformanceTracing;
using YetiCommon.Tests.PerformanceTracing.TestSupport;
using YetiVSI.DebugEngine;
using YetiVSI.Metrics;
using YetiVSI.Shared.Metrics;
using YetiVSI.Test.Metrics.TestSupport;
using ExpressionEvaluation =
    YetiVSI.Shared.Metrics.VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation;
using ExpressionEvaluationStep =
    YetiVSI.Shared.Metrics.VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types.
    ExpressionEvaluationStep;
using Step = YetiVSI.Metrics.ExpressionEvaluationRecorder.StepsRecorder.Step;

namespace YetiVSI.Test.Metrics
{
    [TestFixture]
    public class ExpressionEvaluationRecorderTests
    {
        const int _batchIntervalMs = 1024;

        BatchEventAggregator<ExpressionEvaluationBatch, ExpressionEvaluationBatchParams,
            ExpressionEvaluationBatchSummary> _batchEventAggregator;

        IMetrics _metrics;
        EventSchedulerFake _eventScheduler;
        ITimeSource _timeSource;

        // Object under test
        ExpressionEvaluationRecorder _expressionEvaluationRecorder;

        [SetUp]
        public void SetUp()
        {
            _metrics = Substitute.For<IMetrics>();
            _eventScheduler = new EventSchedulerFake();
            var eventSchedulerFactory = Substitute.For<IEventSchedulerFactory>();
            eventSchedulerFactory
                .Create(Arg.Do<System.Action>(a => _eventScheduler.Callback = a), Arg.Any<int>())
                .Returns(_eventScheduler);
            _timeSource = new MonotonicTimeSource();
            var exceptionRecorder = new ExceptionRecorder(_metrics);
            _batchEventAggregator =
                new BatchEventAggregator<ExpressionEvaluationBatch, ExpressionEvaluationBatchParams,
                    ExpressionEvaluationBatchSummary>(_batchIntervalMs, eventSchedulerFactory,
                                                      exceptionRecorder);

            _expressionEvaluationRecorder =
                new ExpressionEvaluationRecorder(_batchEventAggregator, _metrics);
        }

        [Test]
        public void RecordSingleEventWithTwoStepsTest()
        {
            const ExpressionEvaluationStrategy strategySource = ExpressionEvaluationStrategy.LLDB;
            const ExpressionEvaluationContext contextSource = ExpressionEvaluationContext.FRAME;

            var stepsRecorder = new ExpressionEvaluationRecorder.StepsRecorder(_timeSource);

            using (Step step = stepsRecorder.NewStep(ExpressionEvaluationEngine.LLDB_VARIABLE_PATH))
            {
                step.Finalize(LLDBErrorCode.ERROR);
            }

            using (Step step = stepsRecorder.NewStep(ExpressionEvaluationEngine.LLDB))
            {
                step.Finalize(LLDBErrorCode.OK);
            }

            const long startTimestampUs = 750;
            const long endTimestampUs = 21562;

            _expressionEvaluationRecorder.Record(strategySource, contextSource, stepsRecorder,
                                                 startTimestampUs, endTimestampUs);

            // Get a copy of the batch summary sent to batchEventAggregator so we can verify
            // that it matches the one being sent to metrics.
            ExpressionEvaluationBatchSummary batchSummary = null;
            _batchEventAggregator.BatchSummaryReady += (_, newSummary) => batchSummary = newSummary;

            _eventScheduler.Increment(_batchIntervalMs);

            const ExpressionEvaluation.Types.Strategy strategyExpected =
                ExpressionEvaluation.Types.Strategy.Lldb;
            const ExpressionEvaluation.Types.Context contextExpected = ExpressionEvaluation.Types
                .Context.Frame;

            Assert.AreEqual(1, batchSummary.Proto.ExpressionEvaluations.Count);
            ExpressionEvaluation received = batchSummary.Proto.ExpressionEvaluations[0];
            Assert.Multiple(() =>
            {
                Assert.AreEqual(strategyExpected, received.Strategy);
                Assert.AreEqual(contextExpected, received.Context);
                Assert.NotNull(received.EvaluationSteps);
                Assert.AreEqual(2, received.EvaluationSteps.Count);
                Assert.AreEqual(startTimestampUs, received.StartTimestampMicroseconds);
                Assert.AreEqual(endTimestampUs, received.EndTimestampMicroseconds);
                Assert.Null(received.NatvisValueId);
            });

            const ExpressionEvaluationStep.Types.Engine firstStepEngineExpected =
                ExpressionEvaluationStep.Types.Engine.LldbVariablePath;
            const ExpressionEvaluationStep.Types.EngineResult firstStepEngineResultExpected =
                ExpressionEvaluationStep.Types.EngineResult.LldbError;
            const ExpressionEvaluationStep.Types.Engine secondStepEngineExpected =
                ExpressionEvaluationStep.Types.Engine.Lldb;
            const ExpressionEvaluationStep.Types.EngineResult secondStepEngineResultExpected =
                ExpressionEvaluationStep.Types.EngineResult.LldbOk;

            ExpressionEvaluationStep firstStep = received.EvaluationSteps[0];
            ExpressionEvaluationStep secondStep = received.EvaluationSteps[1];
            Assert.Multiple(() =>
            {
                Assert.AreEqual(firstStepEngineExpected, firstStep.Engine);
                Assert.AreEqual(firstStepEngineResultExpected, firstStep.Result);
                Assert.AreEqual(1, firstStep.DurationMicroseconds);
                Assert.AreEqual(secondStepEngineExpected, secondStep.Engine);
                Assert.AreEqual(secondStepEngineResultExpected, secondStep.Result);
                Assert.AreEqual(1, secondStep.DurationMicroseconds);
            });

            _metrics.Received(1)
                .RecordEvent(DeveloperEventType.Types.Type.VsiDebugExpressionEvaluationBatch,
                             new DeveloperLogEvent
                             {
                                 DebugExpressionEvaluationBatch = batchSummary.Proto,
                                 StatusCode = DeveloperEventStatus.Types.Code.Success
                             });
        }

        [Test]
        public void RecordSingleEventWithValueContextTest()
        {
            const ExpressionEvaluationStrategy strategySource =
                ExpressionEvaluationStrategy.LLDB_EVAL;
            const ExpressionEvaluationContext contextSource = ExpressionEvaluationContext.VALUE;

            var stepsRecorder = new ExpressionEvaluationRecorder.StepsRecorder(_timeSource);

            using (Step step = stepsRecorder.NewStep(ExpressionEvaluationEngine.LLDB_EVAL))
            {
                step.Finalize(LldbEvalErrorCode.Ok);
            }

            const long startTimestampUs = 750;
            const long endTimestampUs = 21562;

            // Attempt to record expression evaluation with context Value, without natvisValueId
            // should throw an exception.
            Assert.Throws<ArgumentException>(() =>
            {
                _expressionEvaluationRecorder.Record(
                    strategySource, contextSource, stepsRecorder,
                    startTimestampUs, endTimestampUs);
            });

            const string natvisValueId = "TestId";

            _expressionEvaluationRecorder.Record(strategySource, contextSource, stepsRecorder,
                                                 startTimestampUs, endTimestampUs, natvisValueId);

            // Get a copy of the batch summary sent to batchEventAggregator so we can verify
            // that it matches the one being sent to metrics.
            ExpressionEvaluationBatchSummary batchSummary = null;
            _batchEventAggregator.BatchSummaryReady += (_, newSummary) => batchSummary = newSummary;

            _eventScheduler.Increment(_batchIntervalMs);

            const ExpressionEvaluation.Types.Strategy strategyExpected =
                ExpressionEvaluation.Types.Strategy.LldbEval;
            const ExpressionEvaluation.Types.Context contextExpected =
                ExpressionEvaluation.Types.Context.Value;

            Assert.AreEqual(1, batchSummary.Proto.ExpressionEvaluations.Count);
            ExpressionEvaluation received = batchSummary.Proto.ExpressionEvaluations[0];
            Assert.Multiple(() =>
            {
                Assert.AreEqual(strategyExpected, received.Strategy);
                Assert.AreEqual(contextExpected, received.Context);
                Assert.NotNull(received.EvaluationSteps);
                Assert.AreEqual(1, received.EvaluationSteps.Count);
                Assert.AreEqual(startTimestampUs, received.StartTimestampMicroseconds);
                Assert.AreEqual(endTimestampUs, received.EndTimestampMicroseconds);
                Assert.AreEqual(natvisValueId, received.NatvisValueId);
            });

            const ExpressionEvaluationStep.Types.Engine stepEngineExpected =
                ExpressionEvaluationStep.Types.Engine.LldbEval;
            const ExpressionEvaluationStep.Types.EngineResult stepEngineResultExpected =
                ExpressionEvaluationStep.Types.EngineResult.LldbEvalOk;

            ExpressionEvaluationStep receivedEvaluationStep = received.EvaluationSteps[0];
            Assert.Multiple(() =>
            {
                Assert.AreEqual(stepEngineExpected, receivedEvaluationStep.Engine);
                Assert.AreEqual(stepEngineResultExpected, receivedEvaluationStep.Result);
                Assert.AreEqual(1, receivedEvaluationStep.DurationMicroseconds);
            });

            _metrics.Received(1)
                .RecordEvent(DeveloperEventType.Types.Type.VsiDebugExpressionEvaluationBatch,
                             new DeveloperLogEvent
                             {
                                 DebugExpressionEvaluationBatch = batchSummary.Proto,
                                 StatusCode = DeveloperEventStatus.Types.Code.Success
                             });
        }

        [Test]
        public void EmptyNatvisValueIdExceptionTest()
        {
            var stepsRecorder = new ExpressionEvaluationRecorder.StepsRecorder(_timeSource);

            Assert.Throws<ArgumentException>(() =>
            {
                _expressionEvaluationRecorder.Record(
                    ExpressionEvaluationStrategy.LLDB,
                    ExpressionEvaluationContext.VALUE,
                    stepsRecorder, startTimestampUs: 750,
                    endTimestampUs: 21562);
            });
        }

        [TestCase(ExpressionEvaluationEngine.LLDB)]
        [TestCase(ExpressionEvaluationEngine.LLDB_VARIABLE_PATH)]
        public void IncompatibleLldbResultExceptionTest(ExpressionEvaluationEngine engine)
        {
            var stepsRecorder = new ExpressionEvaluationRecorder.StepsRecorder(_timeSource);

            Step step = stepsRecorder.NewStep(engine);
            Assert.Throws<ArgumentException>(() => { step.Finalize(LldbEvalErrorCode.Ok); });
        }

        [Test]
        public void IncompatibleLldbEvalResultExceptionTest()
        {
            var stepsRecorder = new ExpressionEvaluationRecorder.StepsRecorder(_timeSource);

            Step step = stepsRecorder.NewStep(ExpressionEvaluationEngine.LLDB_EVAL);
            Assert.Throws<ArgumentException>(() => { step.Finalize(LLDBErrorCode.OK); });
        }
    }
}
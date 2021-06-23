using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DebuggerApi;
using NSubstitute;
using NUnit.Framework;
using YetiCommon.ExceptionRecorder;
using YetiVSI.DebugEngine;
using YetiVSI.Metrics;
using YetiVSI.Shared.Metrics;
using YetiVSI.Test.Metrics.TestSupport;

namespace YetiVSI.Test.Metrics
{
    class ExpressionEvaluationBatchEventAggregatorTests
    {
        const int _batchIntervalMs = 1024;

        EventSchedulerFake _eventScheduler;
        IMetrics _metrics;
        IExceptionRecorder _exceptionRecorder;

        // Object under test
        BatchEventAggregator<ExpressionEvaluationBatch, ExpressionEvaluationBatchParams,
            ExpressionEvaluationBatchSummary> _batchEventAggregator;

        [SetUp]
        public void SetUp()
        {
            _eventScheduler = new EventSchedulerFake();
            var eventSchedulerFactory = Substitute.For<IEventSchedulerFactory>();
            eventSchedulerFactory.Create(Arg.Do<System.Action>(a => _eventScheduler.Callback = a),
                                         _eventScheduler.Interval = _batchIntervalMs)
                .Returns(_eventScheduler);
            _metrics = Substitute.For<IMetrics>();
            _exceptionRecorder = new ExceptionRecorder(_metrics);
            _batchEventAggregator =
                new BatchEventAggregator<ExpressionEvaluationBatch, ExpressionEvaluationBatchParams,
                    ExpressionEvaluationBatchSummary>(_batchIntervalMs, eventSchedulerFactory,
                                                      _exceptionRecorder);
        }

        [Test]
        public void AddExpressionEvaluationEventsTest()
        {
            ExpressionEvaluationBatchSummary batchSummary = null;
            _batchEventAggregator.BatchSummaryReady += (_, newSummary) => batchSummary = newSummary;

            var strategy = ExpressionEvaluationStrategy.LLDB_EVAL_WITH_FALLBACK;
            var context = ExpressionEvaluationContext.FRAME;

            var stepEngine = ExpressionEvaluationEngine.LLDB_EVAL;
            var stepErrorCode = LldbEvalErrorCode.Ok;
            var stepDurationUs = 200;
            var step =
                new ExpressionEvaluationStepBatchParams(stepEngine, stepErrorCode, stepDurationUs);
            var steps = new List<ExpressionEvaluationStepBatchParams>();
            steps.Add(step);

            var startTimestamp = 200;
            var endTimestamp = 400;

            _batchEventAggregator.Add(
                new ExpressionEvaluationBatchParams(strategy, context, steps, startTimestamp,
                                                    endTimestamp, null));
            _eventScheduler.Increment(_batchIntervalMs / 2);
            Assert.IsNull(batchSummary);

            startTimestamp = endTimestamp;
            endTimestamp = startTimestamp + 100;

            _batchEventAggregator.Add(
                new ExpressionEvaluationBatchParams(strategy, context, steps, startTimestamp,
                                                    endTimestamp, null));
            _eventScheduler.Increment(_batchIntervalMs);
            Assert.NotNull(batchSummary);
        }

        [Test]
        public void ExceptionRecordedWithInvalidContextTest()
        {
            ExpressionEvaluationBatchSummary batchSummary = null;
            _batchEventAggregator.BatchSummaryReady += (_, newSummary) => batchSummary = newSummary;

            var strategy = ExpressionEvaluationStrategy.LLDB_EVAL_WITH_FALLBACK;
            // Non-valid context
            var context = (ExpressionEvaluationContext) 1000;

            var stepEngine = ExpressionEvaluationEngine.LLDB_EVAL;
            var stepErrorCode = LldbEvalErrorCode.Ok;
            var stepDurationUs = 200;
            var step =
                new ExpressionEvaluationStepBatchParams(stepEngine, stepErrorCode, stepDurationUs);
            var steps = new List<ExpressionEvaluationStepBatchParams>();
            steps.Add(step);

            var startTimestamp = 200;
            var endTimestamp = 400;

            _batchEventAggregator.Add(
                new ExpressionEvaluationBatchParams(strategy, context, steps, startTimestamp,
                                                    endTimestamp, null));
            _eventScheduler.Increment(_batchIntervalMs);
            Assert.IsNull(batchSummary);

            var batchAggregatorTypeInfo = _batchEventAggregator.GetType().GetProto();
            var exceptionsData = new VSIExceptionData
            {
                CatchSite = new VSIMethodInfo()
                {
                    NamespaceName = batchAggregatorTypeInfo.NamespaceName,
                    ClassName = batchAggregatorTypeInfo.ClassName,
                    MethodName = "HandleBatchCheck"
                }
            };
            exceptionsData.ExceptionsChain.Add(new VSIExceptionData.Types.Exception
            {
                ExceptionType = typeof(ArgumentException).GetProto()
            });

            var logEvent = new DeveloperLogEvent
            {
                StatusCode = DeveloperEventStatus.Types.Code.InternalError
            };
            logEvent.ExceptionsData.Add(exceptionsData);

            _metrics.Received(1)
                .RecordEvent(DeveloperEventType.Types.Type.VsiException, logEvent);
        }
    }
}
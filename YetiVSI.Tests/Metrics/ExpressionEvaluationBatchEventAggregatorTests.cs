using System;
using System.Collections.Generic;
using DebuggerApi;
using Metrics.Shared;
using NSubstitute;
using NUnit.Framework;
using YetiCommon.ExceptionRecorder;
using YetiVSI.DebugEngine;
using YetiVSI.Metrics;
using YetiVSI.Test.Metrics.TestSupport;

namespace YetiVSI.Test.Metrics
{
    class ExpressionEvaluationBatchEventAggregatorTests
    {
        const int _batchIntervalMs = 1024;
        const int _maxExceptionsChainLength = 2;
        const int _maxStackTraceFrames = 2;

        EventSchedulerFake _eventScheduler;
        IVsiMetrics _metrics;
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
            _metrics = Substitute.For<IVsiMetrics>();
            _exceptionRecorder =
                new ExceptionRecorder(_metrics, null, _maxExceptionsChainLength,
                                      _maxStackTraceFrames);
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
            var steps = new List<ExpressionEvaluationStepBatchParams> { step };

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
            var context = (ExpressionEvaluationContext)1000;

            var stepEngine = ExpressionEvaluationEngine.LLDB_EVAL;
            var stepErrorCode = LldbEvalErrorCode.Ok;
            var stepDurationUs = 200;
            var step =
                new ExpressionEvaluationStepBatchParams(stepEngine, stepErrorCode, stepDurationUs);
            var steps = new List<ExpressionEvaluationStepBatchParams> { step };

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

            var firstExceptionInChain = new VSIExceptionData.Types.Exception
            {
                ExceptionType = typeof(ArgumentException).GetProto()
            };

            var firstStackTraceFrame = new VSIExceptionData.Types.Exception.Types.StackTraceFrame
            {
                AllowedNamespace = true,
                Method = new VSIMethodInfo
                {
                    NamespaceName = "YetiVSI.Metrics",
                    ClassName = "ExpressionEvaluationBatchParams",
                    MethodName = "GetContextProto"
                },
                Filename = "ExpressionEvaluationBatch.cs",
                // Set to zero to ignore this value. See RemoveExceptionDataLineNumber method.
                LineNumber = 0
            };
            var secondStackTraceFrame = new VSIExceptionData.Types.Exception.Types.StackTraceFrame
            {
                AllowedNamespace = true,
                Method = new VSIMethodInfo
                {
                    NamespaceName = "YetiVSI.Metrics",
                    ClassName = "ExpressionEvaluationBatchParams",
                    MethodName = "ConvertToProto"
                },
                Filename = "ExpressionEvaluationBatch.cs",
                // Set to zero to ignore this value. See RemoveExceptionDataLineNumber method.
                LineNumber = 0
            };
            firstExceptionInChain.ExceptionStackTraceFrames.Add(firstStackTraceFrame);
            firstExceptionInChain.ExceptionStackTraceFrames.Add(secondStackTraceFrame);
            exceptionsData.ExceptionsChain.Add(firstExceptionInChain);

            var expectedLogEvent = new DeveloperLogEvent
            {
                StatusCode = DeveloperEventStatus.Types.Code.InternalError
            };
            expectedLogEvent.ExceptionsData.Add(exceptionsData);

            _metrics.Received(1)
                .RecordEvent(DeveloperEventType.Types.Type.VsiException,
                             Arg.Is<DeveloperLogEvent>(logEvent =>
                                                           RemoveExceptionDataLineNumber(logEvent)
                                                               .Equals(expectedLogEvent)));
        }

        // Set the line numbers in the ExceptionStackTraceFrames to zero. This is needed as the
        // information of the LineNumber retrieved from the StackFrame LineNumber is not always
        // accurate. See (internal).
        DeveloperLogEvent RemoveExceptionDataLineNumber(DeveloperLogEvent logEvent)
        {
            DeveloperLogEvent copyLogEvent = logEvent.Clone();

            foreach (var exceptionData in copyLogEvent.ExceptionsData)
            {
                foreach (var exception in exceptionData.ExceptionsChain)
                {
                    foreach (var stackTraceFrame in exception.ExceptionStackTraceFrames)
                    {
                        stackTraceFrame.LineNumber = 0;
                    }
                }
            }

            return copyLogEvent;
        }
    }
}
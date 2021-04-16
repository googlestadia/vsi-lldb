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

using DebuggerApi;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using YetiVSI.DebugEngine;
using YetiVSI.Metrics;
using ExpressionEvaluation =
    YetiVSI.Shared.Metrics.VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation;
using ExpressionEvaluationStep =
    YetiVSI.Shared.Metrics.VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types.
    ExpressionEvaluationStep;

namespace YetiVSI.Test.Metrics
{
    [TestFixture]
    public class ExpressionEvaluationBatchTests
    {
        // Object under test
        ExpressionEvaluationBatch _expressionEvaluationBatch;

        [SetUp]
        public void SetUp()
        {
            _expressionEvaluationBatch = new ExpressionEvaluationBatch();
        }

        [Test]
        public void TestEmptyBatch()
        {
            ExpressionEvaluationBatchSummary expressionEvaluationSummary =
                _expressionEvaluationBatch.GetSummary();

            Assert.Zero(expressionEvaluationSummary.Proto.ExpressionEvaluations.Count);
        }

        [Test]
        public void TestSingleEventBatch()
        {
            var strategySource = ExpressionEvaluationStrategy.LLDB;
            var strategyExpected = ExpressionEvaluation.Types.Strategy.Lldb;

            var contextSource = ExpressionEvaluationContext.FRAME;
            var contextExpected = ExpressionEvaluation.Types.Context.Frame;

            var firstStepEngineSource = ExpressionEvaluationEngine.LLDB_VARIABLE_PATH;
            var firstStepEngineExpected = ExpressionEvaluationStep.Types.Engine.LldbVariablePath;
            var firstStepEngineResultSource = LLDBErrorCode.ERROR;
            var firstStepEngineResultExpected =
                ExpressionEvaluationStep.Types.EngineResult.LldbError;
            const long firstStepDurationUs = 400;

            var secondStepEngineSource = ExpressionEvaluationEngine.LLDB;
            var secondStepEngineExpected = ExpressionEvaluationStep.Types.Engine.Lldb;
            var secondStepEngineResultSource = LLDBErrorCode.OK;
            var secondStepEngineResultExpected = ExpressionEvaluationStep.Types.EngineResult.LldbOk;
            const long secondStepDurationUs = 16500;

            var steps = new List<ExpressionEvaluationStepBatchParams>
            {
                new ExpressionEvaluationStepBatchParams(firstStepEngineSource,
                                                        firstStepEngineResultSource,
                                                        firstStepDurationUs),
                new ExpressionEvaluationStepBatchParams(secondStepEngineSource,
                                                        secondStepEngineResultSource,
                                                        secondStepDurationUs)
            };

            const long startTimestampUs = 750;
            const long endTimestampUs = 21562;

            _expressionEvaluationBatch.Add(new ExpressionEvaluationBatchParams(
                                               strategySource, contextSource, steps,
                                               startTimestampUs, endTimestampUs, null));

            ExpressionEvaluationBatchSummary expressionEvaluationSummary =
                _expressionEvaluationBatch.GetSummary();

            Assert.AreEqual(1, expressionEvaluationSummary.Proto.ExpressionEvaluations.Count);
            var expressionEvaluation = expressionEvaluationSummary.Proto.ExpressionEvaluations[0];

            Assert.Multiple(() =>
            {
                Assert.NotNull(expressionEvaluation.Strategy);
                Assert.AreEqual(strategyExpected, expressionEvaluation.Strategy);
                Assert.NotNull(expressionEvaluation.Context);
                Assert.AreEqual(contextExpected, expressionEvaluation.Context);
                Assert.NotNull(expressionEvaluation.EvaluationSteps);
                Assert.AreEqual(startTimestampUs, expressionEvaluation.StartTimestampMicroseconds);
                Assert.AreEqual(endTimestampUs, expressionEvaluation.EndTimestampMicroseconds);
                Assert.Null(expressionEvaluation.NatvisValueId);
            });

            Assert.AreEqual(2, expressionEvaluation.EvaluationSteps.Count);
            var firstStep = expressionEvaluation.EvaluationSteps[0];
            var secondStep = expressionEvaluation.EvaluationSteps[1];
            Assert.Multiple(() =>
            {
                Assert.NotNull(firstStep.Engine);
                Assert.AreEqual(firstStepEngineExpected, firstStep.Engine);
                Assert.NotNull(firstStep.Result);
                Assert.AreEqual(firstStepEngineResultExpected, firstStep.Result);
                Assert.AreEqual(firstStepDurationUs, firstStep.DurationMicroseconds);
                Assert.NotNull(secondStep.Engine);
                Assert.AreEqual(secondStepEngineExpected, secondStep.Engine);
                Assert.NotNull(secondStep.Result);
                Assert.AreEqual(secondStepEngineResultExpected, secondStep.Result);
                Assert.AreEqual(secondStepDurationUs, secondStep.DurationMicroseconds);
            });
        }

        [TestCase(ExpressionEvaluationStrategy.LLDB, ExpressionEvaluation.Types.Strategy.Lldb)]
        [TestCase(ExpressionEvaluationStrategy.LLDB_EVAL,
                  ExpressionEvaluation.Types.Strategy.LldbEval)]
        [TestCase(ExpressionEvaluationStrategy.LLDB_EVAL_WITH_FALLBACK,
                  ExpressionEvaluation.Types.Strategy.LldbEvalWithFallback)]
        public void StrategyBatchTest(ExpressionEvaluationStrategy strategySource,
                                      ExpressionEvaluation.Types.Strategy strategyExpected)
        {
            _expressionEvaluationBatch.Add(new ExpressionEvaluationBatchParams(
                                               strategySource, ExpressionEvaluationContext.FRAME,
                                               new List<ExpressionEvaluationStepBatchParams>(), 500,
                                               2000, null));

            var batchSummary = _expressionEvaluationBatch.GetSummary();
            Assert.NotNull(batchSummary.Proto.ExpressionEvaluations);
            Assert.AreEqual(1, batchSummary.Proto.ExpressionEvaluations.Count);

            var expressionEvaluation = batchSummary.Proto.ExpressionEvaluations[0];
            Assert.AreEqual(strategyExpected, expressionEvaluation.Strategy);
        }

        [TestCase(ExpressionEvaluationContext.FRAME, ExpressionEvaluation.Types.Context.Frame)]
        [TestCase(ExpressionEvaluationContext.VALUE, ExpressionEvaluation.Types.Context.Value)]
        public void ContextBatchTest(ExpressionEvaluationContext contextSource,
                                     ExpressionEvaluation.Types.Context contextExpected)
        {
            _expressionEvaluationBatch.Add(new ExpressionEvaluationBatchParams(
                                               ExpressionEvaluationStrategy.LLDB, contextSource,
                                               new List<ExpressionEvaluationStepBatchParams>(), 500,
                                               2000, null));

            var batchSummary = _expressionEvaluationBatch.GetSummary();
            Assert.NotNull(batchSummary.Proto.ExpressionEvaluations);
            Assert.AreEqual(1, batchSummary.Proto.ExpressionEvaluations.Count);

            var expressionEvaluation = batchSummary.Proto.ExpressionEvaluations[0];
            Assert.AreEqual(contextExpected, expressionEvaluation.Context);
        }

        [TestCase(ExpressionEvaluationEngine.LLDB, ExpressionEvaluationStep.Types.Engine.Lldb)]
        [TestCase(ExpressionEvaluationEngine.LLDB_VARIABLE_PATH,
                  ExpressionEvaluationStep.Types.Engine.LldbVariablePath)]
        [TestCase(ExpressionEvaluationEngine.LLDB_EVAL,
                  ExpressionEvaluationStep.Types.Engine.LldbEval)]
        public void EngineStepBatchTest(ExpressionEvaluationEngine engineSource,
                                        ExpressionEvaluationStep.Types.Engine engineExpected)
        {
            var step =
                new ExpressionEvaluationStepBatchParams(engineSource, LLDBErrorCode.ERROR, 500);
            var steps = new List<ExpressionEvaluationStepBatchParams> { step };

            var batchParams = new ExpressionEvaluationBatchParams(
                ExpressionEvaluationStrategy.LLDB, ExpressionEvaluationContext.FRAME, steps, 500,
                2000, null);
            _expressionEvaluationBatch.Add(batchParams);

            var batchSummary = _expressionEvaluationBatch.GetSummary();
            Assert.NotNull(batchSummary.Proto.ExpressionEvaluations);
            Assert.AreEqual(1, batchSummary.Proto.ExpressionEvaluations.Count);

            var expressionEvaluation = batchSummary.Proto.ExpressionEvaluations[0];
            Assert.NotNull(expressionEvaluation.EvaluationSteps);
            Assert.AreEqual(1, expressionEvaluation.EvaluationSteps.Count);

            var firstStep = expressionEvaluation.EvaluationSteps[0];
            Assert.AreEqual(engineExpected, firstStep.Engine);
        }

        [TestCase(LLDBErrorCode.OK, ExpressionEvaluationStep.Types.EngineResult.LldbOk)]
        [TestCase(LLDBErrorCode.ERROR, ExpressionEvaluationStep.Types.EngineResult.LldbError)]
        public void LldbEvaluationResultBatchTest(LLDBErrorCode resultSource,
                                                  ExpressionEvaluationStep.Types.EngineResult
                                                      resultExpected)
        {
            var step =
                new ExpressionEvaluationStepBatchParams(ExpressionEvaluationEngine.LLDB,
                                                        resultSource, 500);
            var steps = new List<ExpressionEvaluationStepBatchParams> { step };

            var batchParams = new ExpressionEvaluationBatchParams(
                ExpressionEvaluationStrategy.LLDB, ExpressionEvaluationContext.FRAME, steps, 500,
                2000, null);
            _expressionEvaluationBatch.Add(batchParams);

            var batchSummary = _expressionEvaluationBatch.GetSummary();
            Assert.NotNull(batchSummary.Proto.ExpressionEvaluations);
            Assert.AreEqual(1, batchSummary.Proto.ExpressionEvaluations.Count);

            var expressionEvaluation = batchSummary.Proto.ExpressionEvaluations[0];
            Assert.NotNull(expressionEvaluation.EvaluationSteps);
            Assert.AreEqual(1, expressionEvaluation.EvaluationSteps.Count);

            var firstStep = expressionEvaluation.EvaluationSteps[0];
            Assert.AreEqual(resultExpected, firstStep.Result);
        }

        [TestCase(LldbEvalErrorCode.Unknown,
                  ExpressionEvaluationStep.Types.EngineResult.LldbEvalUnknown)]
        [TestCase(LldbEvalErrorCode.Ok, ExpressionEvaluationStep.Types.EngineResult.LldbEvalOk)]
        [TestCase(LldbEvalErrorCode.InvalidExpressionSyntax,
                  ExpressionEvaluationStep.Types.EngineResult.LldbEvalInvalidExpressionSyntax)]
        [TestCase(LldbEvalErrorCode.InvalidNumericLiteral,
                  ExpressionEvaluationStep.Types.EngineResult.LldbEvalInvalidNumericLiteral)]
        [TestCase(LldbEvalErrorCode.InvalidOperandType,
                  ExpressionEvaluationStep.Types.EngineResult.LldbEvalInvalidOperandType)]
        [TestCase(LldbEvalErrorCode.UndeclaredIdentifier,
                  ExpressionEvaluationStep.Types.EngineResult.LldbEvalUndeclaredIdentifier)]
        [TestCase(LldbEvalErrorCode.NotImplemented,
                  ExpressionEvaluationStep.Types.EngineResult.LldbEvalNotImplemented)]
        public void LldbEvalEvaluationResultBatchTest(LldbEvalErrorCode resultSource,
                                                      ExpressionEvaluationStep.Types.EngineResult
                                                          resultExpected)
        {
            var step =
                new ExpressionEvaluationStepBatchParams(ExpressionEvaluationEngine.LLDB_EVAL,
                                                        resultSource, 500);
            var steps = new List<ExpressionEvaluationStepBatchParams> { step };

            var batchParams = new ExpressionEvaluationBatchParams(
                ExpressionEvaluationStrategy.LLDB, ExpressionEvaluationContext.FRAME, steps, 500,
                2000, null);
            _expressionEvaluationBatch.Add(batchParams);

            var batchSummary = _expressionEvaluationBatch.GetSummary();
            Assert.NotNull(batchSummary.Proto.ExpressionEvaluations);
            Assert.AreEqual(1, batchSummary.Proto.ExpressionEvaluations.Count);

            var expressionEvaluation = batchSummary.Proto.ExpressionEvaluations[0];
            Assert.NotNull(expressionEvaluation.EvaluationSteps);
            Assert.AreEqual(1, expressionEvaluation.EvaluationSteps.Count);

            var firstStep = expressionEvaluation.EvaluationSteps[0];
            Assert.AreEqual(resultExpected, firstStep.Result);
        }

        [Test]
        public void AllStrategyValuesMappedTest()
        {
            var enumValues = Enum.GetValues(typeof(ExpressionEvaluationStrategy));
            var steps = new List<ExpressionEvaluationStepBatchParams>();

            // Validate that all strategy values defined in ExpressionEvaluationStrategy are mapped.
            foreach (ExpressionEvaluationStrategy value in enumValues)
            {
                var batchParams = new ExpressionEvaluationBatchParams(
                    value, ExpressionEvaluationContext.FRAME, steps, 500, 2000, null);

                _expressionEvaluationBatch.Add(batchParams);

                Assert.DoesNotThrow(() => { _expressionEvaluationBatch.GetSummary(); });
            }
        }

        [Test]
        public void AllContextValuesMappedTest()
        {
            Array enumValues = Enum.GetValues(typeof(ExpressionEvaluationContext));
            var steps = new List<ExpressionEvaluationStepBatchParams>();

            // Validate that all context values defined in ExpressionEvaluationContext are mapped.
            foreach (ExpressionEvaluationContext value in enumValues)
            {
                var batchParams = new ExpressionEvaluationBatchParams(
                    ExpressionEvaluationStrategy.LLDB, value, steps, 500, 2000, null);

                _expressionEvaluationBatch.Add(batchParams);

                Assert.DoesNotThrow(() => { _expressionEvaluationBatch.GetSummary(); });
            }
        }

        [Test]
        public void AllEngineValuesMappedTest()
        {
            Array enumValues = Enum.GetValues(typeof(ExpressionEvaluationEngine));

            // Validate that all engine values defined in ExpressionEvaluationEngine are mapped.
            foreach (ExpressionEvaluationEngine value in enumValues)
            {
                var step = new ExpressionEvaluationStepBatchParams(value, LLDBErrorCode.OK, 500);
                var steps = new List<ExpressionEvaluationStepBatchParams> { step };
                var batchParams = new ExpressionEvaluationBatchParams(
                    ExpressionEvaluationStrategy.LLDB, ExpressionEvaluationContext.FRAME, steps,
                    500, 2000, null);

                _expressionEvaluationBatch.Add(batchParams);

                Assert.DoesNotThrow(() => { _expressionEvaluationBatch.GetSummary(); });
            }
        }

        [Test]
        public void AllEngineResultValuesMappedTest()
        {
            Array enumValuesLldb = Enum.GetValues(typeof(LLDBErrorCode));

            // Validate that all engine result values for lldb are mapped.
            foreach (LLDBErrorCode value in enumValuesLldb)
            {
                var step =
                    new ExpressionEvaluationStepBatchParams(ExpressionEvaluationEngine.LLDB, value,
                                                            500);
                var steps = new List<ExpressionEvaluationStepBatchParams> { step };
                var batchParams = new ExpressionEvaluationBatchParams(
                    ExpressionEvaluationStrategy.LLDB, ExpressionEvaluationContext.FRAME, steps,
                    500, 2000, null);

                _expressionEvaluationBatch.Add(batchParams);

                Assert.DoesNotThrow(() => { _expressionEvaluationBatch.GetSummary(); });
            }

            Array enumValuesLldbEval = Enum.GetValues(typeof(LldbEvalErrorCode));

            // Validate that all engine result values for lldb-eval are mapped.
            foreach (LldbEvalErrorCode value in enumValuesLldbEval)
            {
                var step =
                    new ExpressionEvaluationStepBatchParams(ExpressionEvaluationEngine.LLDB_EVAL,
                                                            value, 500);
                var steps = new List<ExpressionEvaluationStepBatchParams> { step };
                var batchParams = new ExpressionEvaluationBatchParams(
                    ExpressionEvaluationStrategy.LLDB, ExpressionEvaluationContext.FRAME, steps,
                    500, 2000, null);

                _expressionEvaluationBatch.Add(batchParams);

                Assert.DoesNotThrow(() => { _expressionEvaluationBatch.GetSummary(); });
            }
        }

        [TestCase(-1)]
        [TestCase(-2)]
        [Test]
        public void InvalidLldbEvalErrorCodeMappedToUnknownTest(LldbEvalErrorCode code)
        {
            var step = new ExpressionEvaluationStepBatchParams(
                ExpressionEvaluationEngine.LLDB_EVAL, code, 500);
            var steps = new List<ExpressionEvaluationStepBatchParams> { step };
            var batchParams = new ExpressionEvaluationBatchParams(
                ExpressionEvaluationStrategy.LLDB_EVAL, ExpressionEvaluationContext.FRAME,
                steps, 500, 2000, null);

            _expressionEvaluationBatch.Add(batchParams);

            var summary = _expressionEvaluationBatch.GetSummary();
            Assert.AreEqual(
                ExpressionEvaluationStep.Types.EngineResult.LldbEvalUnknown,
                summary.Proto.ExpressionEvaluations[0].EvaluationSteps[0].Result.Value);
        }
    }
}
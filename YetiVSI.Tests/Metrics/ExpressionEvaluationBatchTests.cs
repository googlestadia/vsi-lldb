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

using NUnit.Framework;
using YetiVSI.Metrics;
using System;
using System.Collections.Generic;
using YetiVSI.Shared.Metrics;

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
            const ExpressionEvaluationBatchParams.Strategy strategy =
                ExpressionEvaluationBatchParams.Strategy.LldbEvalWithFallback;
            const ExpressionEvaluationBatchParams.Context context =
                ExpressionEvaluationBatchParams.Context.Frame;

            const ExpressionEvaluationStep.Engine firstStepEngine =
                ExpressionEvaluationStep.Engine.LldbEval;
            const ExpressionEvaluationStep.EngineResult firstStepEngineResult =
                ExpressionEvaluationStep.EngineResult.LldbEvalNotImplemented;
            const long firstStepDuration = 500;
            const ExpressionEvaluationStep.Engine secondStepEngine =
                ExpressionEvaluationStep.Engine.Lldb;
            const ExpressionEvaluationStep.EngineResult secondStepEngineResult =
                ExpressionEvaluationStep.EngineResult.LldbOk;
            const long secondStepDuration = 2500;
            var steps = new List<ExpressionEvaluationStep>
            {
                new ExpressionEvaluationStep(firstStepEngine, firstStepEngineResult,
                                             firstStepDuration),
                new ExpressionEvaluationStep(secondStepEngine, secondStepEngineResult,
                                             secondStepDuration)
            };
            const long startTimestamp = 7410;
            const long endTimestamp = 11410;

            _expressionEvaluationBatch.Add(
                new ExpressionEvaluationBatchParams(strategy, context, steps, startTimestamp,
                                                    endTimestamp, null));

            ExpressionEvaluationBatchSummary expressionEvaluationSummary =
                _expressionEvaluationBatch.GetSummary();

            Assert.AreEqual(1, expressionEvaluationSummary.Proto.ExpressionEvaluations.Count);
            var expressionEvaluation = expressionEvaluationSummary.Proto.ExpressionEvaluations[0];

            Assert.Multiple(() =>
            {
                Assert.NotNull(expressionEvaluation.Strategy);
                Assert.AreEqual((int) strategy, (int) expressionEvaluation.Strategy);
                Assert.NotNull(expressionEvaluation.Context);
                Assert.AreEqual((int) context, (int) expressionEvaluation.Context);
                Assert.NotNull(expressionEvaluation.EvaluationSteps);
                Assert.AreEqual(startTimestamp, expressionEvaluation.StartTimestampMicroseconds);
                Assert.AreEqual(endTimestamp, expressionEvaluation.EndTimestampMicroseconds);
                Assert.Null(expressionEvaluation.NatvisValueId);
            });

            Assert.AreEqual(2, expressionEvaluation.EvaluationSteps.Count);
            var firstStep = expressionEvaluation.EvaluationSteps[0];
            var secondStep = expressionEvaluation.EvaluationSteps[1];
            Assert.Multiple(() =>
            {
                Assert.NotNull(firstStep.Engine);
                Assert.AreEqual((int) firstStepEngine, (int) firstStep.Engine);
                Assert.NotNull(firstStep.Result);
                Assert.AreEqual((int) firstStepEngineResult, (int) firstStep.Result);
                Assert.AreEqual(firstStepDuration, firstStep.DurationMicroseconds);
                Assert.NotNull(secondStep.Engine);
                Assert.AreEqual((int) secondStepEngine, (int) secondStep.Engine);
                Assert.NotNull(secondStep.Result);
                Assert.AreEqual((int) secondStepEngineResult, (int) secondStep.Result);
                Assert.AreEqual(secondStepDuration, secondStep.DurationMicroseconds);
            });
        }

        [TestCase(
            typeof(VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types.Strategy),
            typeof(ExpressionEvaluationBatchParams.Strategy))]
        [TestCase(
            typeof(VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types.Context),
            typeof(ExpressionEvaluationBatchParams.Context))]
        [TestCase(
            typeof(VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types.
                ExpressionEvaluationStep.Types.Engine), typeof(ExpressionEvaluationStep.Engine))]
        [TestCase(
            typeof(VSIDebugExpressionEvaluationBatch.Types.ExpressionEvaluation.Types.
                ExpressionEvaluationStep.Types.EngineResult),
            typeof(ExpressionEvaluationStep.EngineResult))]
        public void ExpressionEvaluationBatchParamsEnumsTest(Type sourceType, Type destinationType)
        {
            string[] sourceEnumNames = Enum.GetNames(sourceType);
            string[] destinationEnumNames = Enum.GetNames(destinationType);

            Assert.AreEqual(sourceEnumNames.Length, destinationEnumNames.Length);
            Assert.AreEqual(sourceEnumNames, destinationEnumNames);
            // Check that enums have the same value
            foreach (string enumName in sourceEnumNames)
            {
                Assert.AreEqual((int) Enum.Parse(sourceType, enumName),
                                (int) Enum.Parse(destinationType, enumName));
            }
        }
    }
}
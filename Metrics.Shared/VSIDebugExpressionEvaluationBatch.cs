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

namespace Metrics.Shared
{
    /// <summary>
    /// This class is a stub for a proto. It uses a constant hash
    /// as it is only required to be able to override equality
    /// for testing purposes.
    /// </summary>
    public class VSIDebugExpressionEvaluationBatch
    {
        public List<Types.ExpressionEvaluation> ExpressionEvaluations { get; set; }

        public VSIDebugExpressionEvaluationBatch()
        {
            ExpressionEvaluations = new List<Types.ExpressionEvaluation>();
        }

        public override int GetHashCode() => 42;

        public override bool Equals(object other) =>
            Equals(other as VSIDebugExpressionEvaluationBatch);

        public bool Equals(VSIDebugExpressionEvaluationBatch other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(other, this))
            {
                return true;
            }

            if (!Equals(other.ExpressionEvaluations, ExpressionEvaluations) &&
                (other.ExpressionEvaluations == null || ExpressionEvaluations == null ||
                    !other.ExpressionEvaluations.SequenceEqual(ExpressionEvaluations)))
            {
                return false;
            }

            return true;
        }

        public VSIDebugExpressionEvaluationBatch Clone()
        {
            var clone = (VSIDebugExpressionEvaluationBatch) MemberwiseClone();
            clone.ExpressionEvaluations =
                new List<Types.ExpressionEvaluation>(ExpressionEvaluations);
            return clone;
        }

        public void MergeFrom(VSIDebugExpressionEvaluationBatch other)
        {
            if (other.ExpressionEvaluations != null)
            {
                if (ExpressionEvaluations == null)
                {
                    ExpressionEvaluations = new List<Types.ExpressionEvaluation>();
                }

                ExpressionEvaluations.AddRange(other.ExpressionEvaluations);
            }
        }

        public class Types
        {
            public class ExpressionEvaluation
            {
                public Types.Strategy? Strategy { get; set; }
                public Types.Context? Context { get; set; }
                public List<Types.ExpressionEvaluationStep> EvaluationSteps { get; set; }
                public long? StartTimestampMicroseconds { get; set; }
                public long? EndTimestampMicroseconds { get; set; }
                public string NatvisValueId { get; set; }

                public ExpressionEvaluation()
                {
                    EvaluationSteps = new List<Types.ExpressionEvaluationStep>();
                }

                public override int GetHashCode() => 42;

                public override bool Equals(object other) => Equals(other as ExpressionEvaluation);

                public bool Equals(ExpressionEvaluation other)
                {
                    if (other == null)
                    {
                        return false;
                    }

                    if (ReferenceEquals(other, this))
                    {
                        return true;
                    }

                    if (!Equals(other.Strategy, Strategy))
                    {
                        return false;
                    }

                    if (!Equals(other.Context, Context))
                    {
                        return false;
                    }

                    if (!Equals(other.EvaluationSteps, EvaluationSteps) &&
                        (other.EvaluationSteps == null || EvaluationSteps == null ||
                            !other.EvaluationSteps.SequenceEqual(EvaluationSteps)))
                    {
                        return false;
                    }

                    if (other.StartTimestampMicroseconds != StartTimestampMicroseconds)
                    {
                        return false;
                    }

                    if (other.EndTimestampMicroseconds != EndTimestampMicroseconds)
                    {
                        return false;
                    }

                    if (other.NatvisValueId != NatvisValueId)
                    {
                        return false;
                    }

                    return true;
                }

                public ExpressionEvaluation Clone()
                {
                    var clone = (ExpressionEvaluation) MemberwiseClone();
                    clone.EvaluationSteps =
                        new List<Types.ExpressionEvaluationStep>(EvaluationSteps);
                    clone.NatvisValueId = string.Copy(NatvisValueId);
                    return clone;
                }

                public class Types
                {
                    public class ExpressionEvaluationStep
                    {
                        public Types.Engine? Engine { get; set; }
                        public Types.EngineResult? Result { get; set; }
                        public long? DurationMicroseconds { get; set; }

                        public override int GetHashCode() => 42;

                        public override bool Equals(object other) =>
                            Equals(other as ExpressionEvaluationStep);

                        public bool Equals(ExpressionEvaluationStep other)
                        {
                            if (other == null)
                            {
                                return false;
                            }

                            if (ReferenceEquals(other, this))
                            {
                                return true;
                            }

                            if (!Equals(other.Engine, Engine))
                            {
                                return false;
                            }

                            if (!Equals(other.Result, Result))
                            {
                                return false;
                            }

                            if (other.DurationMicroseconds != DurationMicroseconds)
                            {
                                return false;
                            }

                            return true;
                        }

                        public ExpressionEvaluationStep Clone() =>
                            (ExpressionEvaluationStep) MemberwiseClone();

                        public class Types
                        {
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
            }
        }
    }
}
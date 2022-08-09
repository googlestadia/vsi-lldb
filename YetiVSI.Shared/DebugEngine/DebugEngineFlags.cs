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

﻿namespace YetiVSI.DebugEngine
{
    /// <summary>
    /// Indicates if faster expression evaluation is preferred over strict correctness.
    /// </summary>
    public enum FastExpressionEvaluation
    {
        DISABLED,
        ENABLED,
    }

    /// <summary>
    /// Strategy used for expression evaluation, whether lldb-eval is preferred over LLDB
    /// (including the fallback option).
    /// </summary>
    public enum ExpressionEvaluationStrategy
    {
        LLDB,
        LLDB_EVAL,
        LLDB_EVAL_WITH_FALLBACK,
    }

    /// <summary>
    /// Context in which an expression is evaluated.
    /// </summary>
    public enum ExpressionEvaluationContext
    {
        FRAME,
        VALUE,
    }

    /// <summary>
    /// Engine with which the evaluation is performed. This depends on the strategy being used.
    /// </summary>
    public enum ExpressionEvaluationEngine
    {
        // Full evaluation with lldb engine. This engine is used for LLDB and
        // LLDB_EVAL_WITH_FALLBACK strategies.
        LLDB,
        // Engine used before the full evaluation with LLDB. This engine is only used when the
        // strategy is LLDB.
        LLDB_VARIABLE_PATH,
        // Evaluation with lldb-eval engine. This engine is used for LLDB_EVAL and
        // LLDB_EVAL_WITH_FALLBACK strategies.
        LLDB_EVAL,
    }

    /// <summary>
    /// Result of an evaluation performed with LLDB or LLDB_VARIABLE_PATH.
    /// </summary>
    public enum LLDBErrorCode
    {
        OK,
        ERROR,
    }

    /// <summary>
    /// Indicates if async interfaces are enabled.
    /// </summary>
    public enum AsyncInterfaces
    {
        DISABLED,
        ENABLED,
    }

    /// <summary>
    /// Indicates if variable visualizer is available.
    /// </summary>
    public enum LLDBVisualizerSupport
    {
        DISABLED,
        BUILT_IN_ONLY,
        ENABLED,
    }
}

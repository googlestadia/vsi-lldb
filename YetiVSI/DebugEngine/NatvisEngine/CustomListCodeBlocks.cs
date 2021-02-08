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

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.DebugEngine.NatvisEngine
{
    /// <summary>
    /// Represents a code block parsed from CustomListItems definition.
    /// </summary>
    interface ICodeBlock
    {
        /// <summary>
        /// Indicates the current state of execution.
        /// </summary>
        BlockState State { get; }

        /// <summary>
        /// Evaluate next instruction in the code block. Evaluation may or may not have result.
        /// Throws ExpressionEvaluationFailed if unable to evaluate an instruction.
        /// </summary>
        Task<EvaluateResult> EvaluateAsync();

        /// <summary>
        /// Clean up the current state of execution and set BlockState.New state to the block.
        /// </summary>
        void Reset();
    }

    enum BlockState
    {
        /// <summary>
        /// Indicates the code block which has not yet started execution.
        /// </summary>
        New,

        /// <summary>
        /// Indicates the code block which execution is in progress.
        /// </summary>
        InProgress,

        /// <summary>
        /// Indicates the code block which has completed the execution.
        /// </summary>
        Done,
    }

    enum ResultType
    {
        /// <summary>
        /// Returned by instructions which have no return type.
        /// </summary>
        None,

        /// <summary>
        /// Returned by instructions which return IVariableInformation.
        /// </summary>
        Var,

        /// <summary>
        /// Returned by instructions which result in Break.
        /// </summary>
        Break,
    }

    /// <summary>
    /// Represents the result of the code block evaluation. Contains ResultType and the value in
    /// case of ResultType.Some.
    /// </summary>
    class EvaluateResult
    {
        EvaluateResult(ResultType type, IVariableInformation data)
        {
            Type = type;
            Data = data;
        }

        public ResultType Type { get; }
        public IVariableInformation Data { get; }

        public static EvaluateResult None() => new EvaluateResult(ResultType.None, null);

        public static EvaluateResult Break() => new EvaluateResult(ResultType.Break, null);

        public static EvaluateResult Some(IVariableInformation data) =>
            new EvaluateResult(ResultType.Var, data);
    }

    /// <summary>
    /// Represents a code block that is guarded by an optional condition (if/else if/else)
    /// </summary>
    class ConditionalCodeBlock
    {
        public string Condition { get; set; }
        public object[] CodeBlock { get; set; }
    }

    /// <summary>
    /// A grouping of conditional code blocks is equivalent to a chain of if/elseif/else.
    /// </summary>
    class ConditionalCodeBlockGroup
    {
        public ConditionalCodeBlockGroup(List<ConditionalCodeBlock> conditionalCode)
        {
            ConditionalCode = conditionalCode;
        }

        public List<ConditionalCodeBlock> ConditionalCode { get; }
    }

    /// <summary>
    /// Parses Natvis definition of CustomListItems instructions and returns IList<ICodeBlock>.
    /// </summary>
    class CodeBlockParser
    {
        readonly NatvisExpressionEvaluator _evaluator;

        public CodeBlockParser(NatvisExpressionEvaluator evaluator)
        {
            _evaluator = evaluator;
        }

        public IList<ICodeBlock> Parse(object[] codeBlocks, CustomListItemsContext ctx)
        {
            var blocks = new List<ICodeBlock>();
            foreach (object codeBlock in CombineConditionalCodeBlocks(codeBlocks))
            {
                if (codeBlock is CustomListItemType customListItemType)
                {
                    blocks.Add(new ItemBlock(customListItemType, ctx, _evaluator));
                }
                else if (codeBlock is ExecType execType)
                {
                    blocks.Add(new ExecBlock(execType, ctx, _evaluator));
                }
                else if (codeBlock is LoopType loopType)
                {
                    blocks.Add(new LoopBlock(loopType, ctx, _evaluator, this));
                }
                else if (codeBlock is BreakType breakType)
                {
                    blocks.Add(new BreakBlock(breakType, ctx, _evaluator));
                }
                else if (codeBlock is ConditionalCodeBlockGroup codeBlockGroup)
                {
                    blocks.Add(new ConditionalBlock(codeBlockGroup, ctx, _evaluator, this));
                }
                else
                {
                    throw new NotSupportedException($"{codeBlock.GetType()} is unknown.");
                }
            }

            return blocks;
        }

        /// <summary>
        /// Combines adjacent conditional code block elements (if/elseif/else) into a single
        /// independent item that can be processed. Other code block types are returned as is.
        /// </summary>
        IEnumerable<object> CombineConditionalCodeBlocks(object[] codeBlock)
        {
            Func<object, bool> isControlFlowType =
                t => (t is IfType) || (t is ElseifType) || (t is ElseType);

            var combinedBlocks = new List<object>();

            for (int i = 0; i < codeBlock.Length; i++)
            {
                object current = codeBlock[i];
                if (!(current is IfType))
                {
                    if (current is ElseifType || current is ElseType)
                    {
                        throw new InvalidOperationException(
                            "<If> must be the first instruction in the conditional blocks " +
                            "sequence");
                    }

                    combinedBlocks.Add(current);
                    continue;
                }

                var conditionalCode = new List<ConditionalCodeBlock>();
                int j = i;

                while (j < codeBlock.Length && isControlFlowType(codeBlock[j]))
                {
                    if (codeBlock[j] is IfType)
                    {
                        var ifType = codeBlock[j] as IfType;
                        conditionalCode.Add(new ConditionalCodeBlock()
                        {
                            Condition = ifType.Condition,
                            CodeBlock = ifType.CodeBlock,
                        });
                    }
                    else if (codeBlock[j] is ElseifType)
                    {
                        var elseifType = codeBlock[j] as ElseifType;
                        conditionalCode.Add(new ConditionalCodeBlock()
                        {
                            Condition = elseifType.Condition,
                            CodeBlock = elseifType.CodeBlock,
                        });
                    }
                    else if (codeBlock[j] is ElseType)
                    {
                        var elseType = codeBlock[j] as ElseType;
                        conditionalCode.Add(new ConditionalCodeBlock()
                        {
                            Condition = null,
                            CodeBlock = elseType.CodeBlock,
                        });
                    }
                    else
                    {
                        throw new NotSupportedException(
                            $"Conditional type not supported: {codeBlock[j]}");
                    }

                    j++;
                }

                combinedBlocks.Add(new ConditionalCodeBlockGroup(conditionalCode));
                i = j - 1;
            }

            return combinedBlocks;
        }
    }

    /// <summary>
    /// Represents a group of ICodeBlock executed one by one until the end, a break instruction or
    /// an error encountered.
    /// </summary>
    class MultipleInstructionsBlock : ICodeBlock
    {
        readonly IList<ICodeBlock> _blocks;

        int _position;

        public MultipleInstructionsBlock(IList<ICodeBlock> blocks)
        {
            _blocks = blocks;
        }

        public BlockState State { get; private set; }

        public async Task<EvaluateResult> EvaluateAsync()
        {
            if (_position >= _blocks.Count)
            {
                State = BlockState.Done;
                return EvaluateResult.None();
            }

            State = BlockState.InProgress;

            ICodeBlock currentBlock = _blocks[_position];
            EvaluateResult result = await currentBlock.EvaluateAsync();

            if (result.Type == ResultType.Break)
            {
                State = BlockState.Done;
            }
            else if (currentBlock.State == BlockState.Done)
            {
                _position++;
            }

            return result;
        }

        public void Reset()
        {
            foreach (ICodeBlock block in _blocks)
            {
                block.Reset();
            }

            _position = 0;
            State = BlockState.New;
        }
    }

    /// <summary>
    /// Represents <Item> instruction from CustomListItems which executes an expression and emits
    /// the result.
    /// </summary>
    class ItemBlock : ICodeBlock
    {
        readonly CustomListItemType _customListItem;
        readonly CustomListItemsContext _ctx;

        readonly NatvisExpressionEvaluator _evaluator;

        public ItemBlock(CustomListItemType customListItem, CustomListItemsContext ctx,
                         NatvisExpressionEvaluator evaluator)
        {
            _customListItem = customListItem;
            _ctx = ctx;
            _evaluator = evaluator;
        }

        public BlockState State { get; private set; }

        public async Task<EvaluateResult> EvaluateAsync()
        {
            if (State == BlockState.Done)
            {
                return EvaluateResult.None();
            }

            State = BlockState.Done;

            if (!await _evaluator.EvaluateConditionAsync(_customListItem.Condition, _ctx.Variable,
                                                         _ctx.NatvisScope))
            {
                return EvaluateResult.None();
            }

            string name = await EvaluateItemNameAsync(_customListItem.Name, _ctx);
            IVariableInformation result = await _evaluator.EvaluateExpressionAsync(
                _customListItem.Value, _ctx.Variable, _ctx.NatvisScope, name);

            return EvaluateResult.Some(result);
        }

        public void Reset() => State = BlockState.New;

        /// <summary>
        /// Evaluates the name of an <Item> node.
        ///
        /// If no Name attribute is present, the resulting name is "[<index>]", where <index>
        /// counts all custom list items, starting at 0. It is increased in all cases, even if a
        /// Name attribute is present or the evaluation fails.
        ///
        /// If a Name attribute is present, the name is evaluated as mixed expression, i.e. a
        /// string that contains text that is intended to be interpreted verbatim and zero or more
        /// expressions to be evaluated. Expressions are contained within a set of curly braces.
        ///
        /// Eg. "Test: {5 + 8}, {17 + 2}" -> "Test: 13, 19"
        ///
        /// Name can include curly braces, to escape them use '{{' and '}}'.
        ///
        /// Eg. "Test: {{{5 + 8}, {17 + 2}}}" -> "Test: {13, 19}"
        ///
        /// Only the first error is captured in the expression result.
        /// </summary>
        async Task<string> EvaluateItemNameAsync(string mixedExpression, CustomListItemsContext ctx)
        {
            int currIndex = ctx.ItemAutoIndex++;

            if (string.IsNullOrEmpty(mixedExpression))
            {
                return $"[{currIndex}]";
            }

            var stringBuilder = new StringBuilder();
            for (int i = 0; i < mixedExpression.Length; i++)
            {
                char c = mixedExpression[i];
                if (c == '{')
                {
                    if (i + 1 < mixedExpression.Length && mixedExpression[i + 1] == '{')
                    {
                        stringBuilder.Append('{');
                        i++;
                        continue;
                    }

                    int j = i + 1;
                    var expressionBuilder = new StringBuilder();
                    while (mixedExpression[j] != '}' && j < mixedExpression.Length)
                    {
                        expressionBuilder.Append(mixedExpression[j]);
                        j++;
                    }

                    IVariableInformation result = await _evaluator.EvaluateExpressionAsync(
                        expressionBuilder.ToString(), _ctx.Variable, _ctx.NatvisScope, null);

                    stringBuilder.Append(await result.ValueAsync());
                    i = j;
                }
                else if (c == '}')
                {
                    // Accept both } and }} as closing braces to match native behavior.
                    stringBuilder.Append('}');
                    if (i + 1 < mixedExpression.Length && mixedExpression[i + 1] == '}')
                    {
                        i++;
                    }
                }
                else
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString();
        }
    }

    /// <summary>
    /// Represents <Exec> instruction from CustomListItems which executes an expression.
    /// </summary>
    class ExecBlock : ICodeBlock
    {
        readonly ExecType _exec;
        readonly CustomListItemsContext _ctx;

        readonly NatvisExpressionEvaluator _evaluator;

        public ExecBlock(ExecType execType, CustomListItemsContext ctx,
                         NatvisExpressionEvaluator evaluator)
        {
            _exec = execType;
            _ctx = ctx;
            _evaluator = evaluator;
        }

        public BlockState State { get; private set; }

        public async Task<EvaluateResult> EvaluateAsync()
        {
            if (State == BlockState.Done)
            {
                return EvaluateResult.None();
            }

            State = BlockState.Done;

            if (await _evaluator.EvaluateConditionAsync(_exec.Condition, _ctx.Variable,
                                                        _ctx.NatvisScope))
            {
                await _evaluator.EvaluateExpressionAsync(_exec.Value, _ctx.Variable,
                                                         _ctx.NatvisScope, null);
            }

            return EvaluateResult.None();
        }

        public void Reset() => State = BlockState.New;
    }

    /// <summary>
    /// Represents <Break> instruction from CustomListItems. According to schema definition, it
    /// breaks out of the innermost <Loop>. In case it is outside of a loop it will terminate the
    /// iteration.
    /// </summary>
    class BreakBlock : ICodeBlock
    {
        readonly BreakType _breakType;
        readonly CustomListItemsContext _ctx;

        readonly NatvisExpressionEvaluator _evaluator;

        public BreakBlock(BreakType breakType, CustomListItemsContext ctx,
                          NatvisExpressionEvaluator evaluator)
        {
            _breakType = breakType;
            _ctx = ctx;
            _evaluator = evaluator;
        }

        public BlockState State { get; private set; }

        public async Task<EvaluateResult> EvaluateAsync()
        {
            if (State == BlockState.Done)
            {
                return EvaluateResult.None();
            }

            State = BlockState.Done;

            return await _evaluator.EvaluateConditionAsync(_breakType.Condition, _ctx.Variable,
                                                           _ctx.NatvisScope)
                       ? EvaluateResult.Break()
                       : EvaluateResult.None();
        }

        public void Reset() => State = BlockState.New;
    }

    /// <summary>
    /// Represents <Loop> instruction from CustomListItems.
    /// </summary>
    class LoopBlock : ICodeBlock
    {
        readonly LoopType _loop;
        readonly CustomListItemsContext _ctx;

        readonly NatvisExpressionEvaluator _evaluator;
        readonly MultipleInstructionsBlock _loopInstructions;

        public LoopBlock(LoopType loopType, CustomListItemsContext ctx,
                         NatvisExpressionEvaluator evaluator, CodeBlockParser parser)
        {
            _loop = loopType;
            _ctx = ctx;
            _evaluator = evaluator;

            IList<ICodeBlock> loopBlocks = parser.Parse(loopType.CodeBlock ?? new object[0], ctx);
            _loopInstructions = new MultipleInstructionsBlock(loopBlocks);
        }

        public BlockState State { get; private set; }

        public async Task<EvaluateResult> EvaluateAsync()
        {
            if (State == BlockState.Done)
            {
                return EvaluateResult.None();
            }

            State = BlockState.InProgress;

            if (_loopInstructions.State == BlockState.Done)
            {
                _loopInstructions.Reset();
            }

            if (_loopInstructions.State == BlockState.New &&
                !await _evaluator.EvaluateConditionAsync(_loop.Condition, _ctx.Variable,
                                                         _ctx.NatvisScope))
            {
                State = BlockState.Done;
                return EvaluateResult.None();
            }

            EvaluateResult result = await _loopInstructions.EvaluateAsync();

            if (result.Type == ResultType.Break)
            {
                State = BlockState.Done;
                return EvaluateResult.None();
            }

            return result;
        }

        public void Reset()
        {
            _loopInstructions.Reset();
            State = BlockState.New;
        }
    }

    /// <summary>
    /// Represents grouped conditional instruction from CustomListItems (<If>, <Elseif>, <Else>).
    /// </summary>
    class ConditionalBlock : ICodeBlock
    {
        readonly ConditionalCodeBlockGroup _conditionGroup;
        readonly CustomListItemsContext _ctx;

        readonly NatvisExpressionEvaluator _evaluator;
        readonly IList<MultipleInstructionsBlock> _innerBlocks;
        MultipleInstructionsBlock _trueBlock;

        public ConditionalBlock(ConditionalCodeBlockGroup conditionGroup,
                                CustomListItemsContext ctx, NatvisExpressionEvaluator evaluator,
                                CodeBlockParser parser)
        {
            _conditionGroup = conditionGroup;
            _ctx = ctx;
            _evaluator = evaluator;

            _innerBlocks = conditionGroup.ConditionalCode
                .Select(branch => new MultipleInstructionsBlock(
                            parser.Parse(branch.CodeBlock ?? new object[0], ctx)))
                .ToList();
        }

        public BlockState State { get; private set; }

        public async Task<EvaluateResult> EvaluateAsync()
        {
            if (State == BlockState.Done)
            {
                return EvaluateResult.None();
            }

            State = BlockState.InProgress;

            if (_trueBlock == null)
            {
                for (int i = 0; i < _conditionGroup.ConditionalCode.Count; i++)
                {
                    ConditionalCodeBlock branch = _conditionGroup.ConditionalCode[i];
                    if (await _evaluator.EvaluateConditionAsync(branch.Condition, _ctx.Variable,
                                                                _ctx.NatvisScope))
                    {
                        _trueBlock = _innerBlocks[i];
                        break;
                    }
                }

                if (_trueBlock == null)
                {
                    State = BlockState.Done;
                    return EvaluateResult.None();
                }
            }

            EvaluateResult result = await _trueBlock.EvaluateAsync();
            if (_trueBlock.State == BlockState.Done)
            {
                State = BlockState.Done;
            }

            return result;
        }

        public void Reset()
        {
            foreach (ICodeBlock block in _innerBlocks)
            {
                block.Reset();
            }

            _trueBlock = null;
            State = BlockState.New;
        }
    }
}
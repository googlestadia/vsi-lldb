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

﻿using System.Collections.Generic;

namespace YetiVSI.DebugEngine.Variables
{

    enum TokenType
    {
        OPEN_BRACKET,
        CLOSE_BRACKET
    }

    /// <summary>
    /// Token defined by its starting position in the string and its type.
    /// </summary>
    class Token
    {
        public TokenType Type { get; private set; }
        public int Position { get; private set; }

        public Token(TokenType type, int position)
        {
            Type = type;
            Position = position;
        }
    }

    /// <summary>
    /// Parser used to detect opening '[' and closing ']' brackets in the expression
    /// considering the context. If the bracket is inside string or comment, it is
    /// not counted as a token.
    /// </summary>
    class ExpressionParser
    {
        enum State
        {
            IN_COMMENT,
            IN_STRING,
            IN_SINGLE_QUOTES,
            NORMAL
        }

        readonly string expr;

        public ExpressionParser(string expression)
        {
            expr = expression;
        }

        /// <summary>
        /// Parses the expression provided in the constructor.
        /// If parsing the expression finished in the unexpected state (not closed comment
        /// or string) then returns false.
        /// </summary>
        public bool Parse(out List<Token> tokens)
        {
            State state = State.NORMAL;
            tokens = new List<Token>();
            for (int i = 0; i < expr.Length; i++)
            {
                if (state == State.IN_COMMENT &&
                    expr[i] == '*' && i + 1 < expr.Length && expr[i + 1] == '/')
                {
                    i++;
                    state = State.NORMAL;
                }
                else if (state == State.IN_STRING)
                {
                    if (expr[i] == '"')
                    {
                        state = State.NORMAL;
                    }
                    // Looking for escaped quotes: skipping \".
                    else if (expr[i] == '\\' && i + 1 < expr.Length)
                    {
                        i++;
                    }
                }
                else if (state == State.IN_SINGLE_QUOTES)
                {
                    if (expr[i] == '\'')
                    {
                        state = State.NORMAL;
                    }
                    // Looking for escaped quote '\''.
                    else if (expr[i] == '\\')
                    {
                        i++;
                    }
                }
                else if (state == State.NORMAL)
                {
                    if (expr[i] == ']')
                    {
                        tokens.Add(new Token(TokenType.CLOSE_BRACKET, i));
                    }
                    else if (expr[i] == '[')
                    {
                        tokens.Add(new Token(TokenType.OPEN_BRACKET, i));
                    }
                    else if (expr[i] == '/' && i + 1 < expr.Length && expr[i + 1] == '*')
                    {
                        i++;
                        state = State.IN_COMMENT;
                    }
                    else if (expr[i] == '"')
                    {
                        state = State.IN_STRING;
                    }
                    else if (expr[i] == '\'')
                    {
                        state = State.IN_SINGLE_QUOTES;
                    }
                }
            }

            return state == State.NORMAL;
        }
    }
}

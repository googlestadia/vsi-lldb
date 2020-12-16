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

﻿using DebuggerApi;
using System.Diagnostics;

namespace YetiVSI.DebugEngine.Variables
{
    /// <summary>
    /// Provides the number of children requested by an array size expression format specifier.
    /// </summary>
    public class ExpressionNumChildrenProvider : IRemoteValueNumChildrenProvider
    {
        readonly string _expression;
        readonly RemoteValue _sizeSpecifierContext;

        public ExpressionNumChildrenProvider(string expression, RemoteValue sizeSpecifierContext)
        {
            _expression = expression;
            _sizeSpecifierContext = sizeSpecifierContext;
        }

        public string Specifier => _expression;

        public uint GetNumChildren(RemoteValue remoteValue)
        {
            var numChildren = remoteValue.GetNumChildren();
            RemoteValue v = (_sizeSpecifierContext == null)
                                ? remoteValue.CreateValueFromExpression("dummy", _expression)
                                : _sizeSpecifierContext.EvaluateExpression(_expression);

            var err = v.GetError();
            if (err.Fail())
            {
                Trace.WriteLine($"ERROR: Failed to resolve size format expression: {_expression}." +
                                $" Reason: {err.GetCString()}.");
                return numChildren;
            }

            uint size;
            if (!uint.TryParse(v.GetValue(ValueFormat.Default), out size))
            {
                Trace.WriteLine($"ERROR: Failed to resolve size format expression: {_expression}." +
                                $" Reason: Expression isn't a uint");
                return numChildren;
            }
            return size;
        }
    }
}

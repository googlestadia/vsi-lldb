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

﻿using Google.VisualStudioFake.API;
using Google.VisualStudioFake.API.UI;
using Microsoft.VisualStudio.Debugger.Interop;
using System;
using System.Collections.Generic;
using YetiCommon.Util;

namespace Google.VisualStudioFake.Internal.UI
{
    // TODO: Implement asynchronous watch expression evaluation.
    public class SyncWatchWindow : IWatchWindow, IVariableDataSource
    {
        readonly IDebugSessionContext _debugSessionContext;
        readonly VariableEntry.Factory _variableEntryFactory;
        readonly IList<IVariableEntryInternal> _watchEntries = new List<IVariableEntryInternal>();
        readonly IDictionary<IVariableEntryInternal, string> _varEntryToExpression =
            new Dictionary<IVariableEntryInternal, string>();

        public SyncWatchWindow(
            IDebugSessionContext debugSessionContext, VariableEntry.Factory variableEntryFactory)
        {
            _debugSessionContext = debugSessionContext;
            _debugSessionContext.SelectedStackFrameChanged +=
                HandleSelectedStackFrameOrHexadecimalDisplayChanged;
            _debugSessionContext.HexadecimalDisplayChanged +=
                HandleSelectedStackFrameOrHexadecimalDisplayChanged;
            _variableEntryFactory = variableEntryFactory;
        }

        #region IWatchWindow

        public IVariableEntry AddWatch(string expression)
        {
            if (_debugSessionContext.ProgramState == ProgramState.Terminated)
            {
                throw new InvalidOperationException(
                    $"Cannot add the watch ({expression}); " +
                    $"program state = {_debugSessionContext.ProgramState}");
            }

            // If this class starts using the decorator pattern, the below use of `this` will need
            // to be reviewed.
            IVariableEntryInternal watch = _variableEntryFactory.Create(this);
            _varEntryToExpression[watch] = expression;
            _watchEntries.Add(watch);
            // TODO: Watch window should update its entries the same way as
            // vanilla VS.
            if (_debugSessionContext.ProgramState == ProgramState.AtBreak)
            {
                watch.Refresh();
            }
            return watch;
        }

        public IList<IVariableEntry> GetWatchEntries() => new List<IVariableEntry>(_watchEntries);

        public IList<IVariableEntry> DeleteAllWatchEntries()
        {
            var deletedEntries = new List<IVariableEntry>(_watchEntries);
            _watchEntries.ForEach(e => e.OnDelete());
            _watchEntries.Clear();
            _varEntryToExpression.Clear();
            return deletedEntries;
        }

        #endregion

        #region IVariableDataSource

        public void GetCurrentState(
            IVariableEntryInternal varEntry, Action<DEBUG_PROPERTY_INFO> callback)
        {
            IDebugExpression2 debugExpression = ParseExpression(_varEntryToExpression[varEntry]);
            IDebugProperty2 debugProperty = EvaluateExpression(debugExpression);
            DEBUG_PROPERTY_INFO propertyInfo = GetPropertyInfo(debugProperty);
            callback(propertyInfo);
        }

        #endregion

        void HandleSelectedStackFrameOrHexadecimalDisplayChanged()
        {
            if (_debugSessionContext.SelectedStackFrame == null)
            {
                _watchEntries.ForEach(e => e.OnReset());
                return;
            }

            // TODO: Watch window should update its entries the same way as
            // vanilla VS.
            _watchEntries.ForEach(e => e.Refresh());
        }

        IDebugProperty2 EvaluateExpression(IDebugExpression2 debugExpression)
        {
            IDebugProperty2 debugProperty;
            // TODO: Provide proper eval flags to IDebugExpression2.EvaluateSync.
            HResultChecker.Check(debugExpression.EvaluateSync(0, 0, null, out debugProperty));
            return debugProperty;
        }

        IDebugExpression2 ParseExpression(string expression)
        {
            IDebugExpression2 debugExpression;
            string errorString;
            uint error;
            HResultChecker.Check(
                GetExpressionContext().ParseText(expression, enum_PARSEFLAGS.PARSE_EXPRESSION,
                Radix, out debugExpression, out errorString, out error));
            if (!string.IsNullOrEmpty(errorString) || error != 0)
            {
                throw new NotSupportedException(
                    $"VSFake doesn't know how to handle the error: ({errorString}, {error})");
            }
            return debugExpression;
        }

        DEBUG_PROPERTY_INFO GetPropertyInfo(IDebugProperty2 debugProperty)
        {
            var infos = new DEBUG_PROPERTY_INFO[1];
            HResultChecker.Check(debugProperty.GetPropertyInfo(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ALL, Radix, 0, null, 0, infos));
            return infos[0];
        }

        IDebugExpressionContext2 GetExpressionContext()
        {
            IDebugExpressionContext2 expressionContext;
            HResultChecker.Check(_debugSessionContext.SelectedStackFrame.GetExpressionContext(
                out expressionContext));
            return expressionContext;
        }

        uint Radix => _debugSessionContext.HexadecimalDisplay ? 16u : 10u;
    }
}

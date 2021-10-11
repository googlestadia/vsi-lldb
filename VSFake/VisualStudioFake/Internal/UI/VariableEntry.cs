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

using Google.VisualStudioFake.API;
using Google.VisualStudioFake.API.UI;
using Google.VisualStudioFake.Internal.Jobs;
using Microsoft.VisualStudio.Debugger.Interop;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Google.VisualStudioFake.Internal.UI
{
    /// <summary>
    /// Provides additional API for internal operations.
    /// </summary>
    public interface IVariableEntryInternal : IVariableEntry
    {
        /// <summary>
        /// Called by the owning entity to reset this variable to a non-evaluated state.
        /// </summary>
        void OnReset();

        /// <summary>
        /// Called by the owning entity to set this variable to a deleted state.
        /// </summary>
        void OnDelete();
    }

    public class VariableEntry : IVariableEntryInternal
    {
        public class Factory
        {
            readonly IJobQueue _jobQueue;
            readonly IDebugSessionContext _debugSessionContext;
            readonly VariableExpander.Factory _variableExpanderFactory;

            public Factory(IJobQueue jobQueue, IDebugSessionContext debugSessionContext,
                           VariableExpander.Factory variableExpanderFactory)
            {
                _jobQueue = jobQueue;
                _debugSessionContext = debugSessionContext;
                _variableExpanderFactory = variableExpanderFactory;
            }

            public virtual IVariableEntryInternal Create(IVariableDataSource dataSource) =>
                new VariableEntry(dataSource, _jobQueue, _debugSessionContext,
                                  _variableExpanderFactory.Create());
        }

        readonly IVariableDataSource _dataSource;
        readonly IJobQueue _jobQueue;
        readonly IDebugSessionContext _debugSessionContext;
        readonly IVariableExpander _variableExpander;

        DEBUG_PROPERTY_INFO _propertyInfo;

        VariableEntry(IVariableDataSource dataSource, IJobQueue jobQueue,
                      IDebugSessionContext debugSessionContext, IVariableExpander variableExpander)
        {
            _dataSource = dataSource;
            _jobQueue = jobQueue;
            _debugSessionContext = debugSessionContext;
            _variableExpander = variableExpander;
        }

        #region IVariableEntry

        public string Fullname => PropertyInfo.bstrFullName;

        public string Name => PropertyInfo.bstrName;

        public string Value => PropertyInfo.bstrValue;

        public string Type => PropertyInfo.bstrType;

        public IVariableEntry Refresh()
        {
            if (State == VariableState.Pending)
            {
                throw new InvalidOperationException("Another refresh operation is still pending.");
            }

            if (_debugSessionContext.SelectedStackFrame == null)
            {
                throw new InvalidOperationException(
                    $"No stack frame selected while trying to refresh {this}.");
            }

            State = VariableState.Pending;
            _jobQueue.Push(new GenericJob(
                               () => _dataSource.GetCurrentState(
                                   this, CurrentStateRetrievalCallback), $"Get variable state"));
            return this;
        }

        public VariableState State { get; private set; } = VariableState.NotEvaluated;

        public bool RefreshRequired => PropertyInfo.dwAttrib.HasFlag(
            enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_ERROR);

        public bool Ready
        {
            get
            {
                switch (State)
                {
                    case VariableState.Evaluated:
                        return true;
                    case VariableState.Pending:
                    case VariableState.NotEvaluated:
                    case VariableState.Deleted:
                        return false;
                    default:
                        throw new InvalidOperationException(
                            $"The variable state ({State}) isn't handled by the " +
                            $"{nameof(Ready)} getter.");
                }
            }
        }

        public override string ToString()
        {
            if (State != VariableState.Evaluated)
            {
                return $"{{state:{State}}}";
            }

            return $"{{name:\"{Name}\", type:\"{Type}\", value:\"{Value}\", state:{State}}}";
        }

        public bool HasSideEffects => PropertyInfo.dwAttrib.HasFlag(
            enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_SIDE_EFFECT);

        public bool HasStringView => PropertyInfo.dwAttrib.HasFlag(
            enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_RAW_STRING);

        public bool IsExpandable => PropertyInfo.dwAttrib.HasFlag(
            enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_OBJ_IS_EXPANDABLE);

        public bool IsReadOnly => PropertyInfo.dwAttrib.HasFlag(
            enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_READONLY);

        public string StringView
        {
            get
            {
                var debugProperty3 = DebugProperty3;
                if (debugProperty3 == null || !HasStringView)
                {
                    return null;
                }

                uint strLen;
                HResultChecker.Check(debugProperty3.GetStringCharLength(out strLen));

                uint numFetched;
                var chars = new ushort[strLen];
                HResultChecker.Check(debugProperty3.GetStringChars(strLen, chars, out numFetched));

                return new string(chars.Select(c => (char) c).ToArray());
            }
        }

        public bool HasError => PropertyInfo.dwAttrib.HasFlag(
            enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_ERROR);

        public IList<IVariableEntry> GetChildren(int offset = 0, int count = int.MaxValue)
        {
            EnsureVaribleHasBeenEvaluated();

            if (!IsExpandable)
            {
                return new List<IVariableEntry>();
            }

            return _variableExpander.GetOrCreateChildren(offset, count);
        }

        #endregion

        #region IVariableEntryInternal

        public void OnReset()
        {
            _propertyInfo = default(DEBUG_PROPERTY_INFO);
            _variableExpander.ResetChildren();
            State = VariableState.NotEvaluated;
        }

        public void OnDelete()
        {
            OnReset();
            _variableExpander.DeleteChildren();
            State = VariableState.Deleted;
        }

        #endregion

        void CurrentStateRetrievalCallback(DEBUG_PROPERTY_INFO propertyInfo)
        {
            _propertyInfo = propertyInfo;
            State = VariableState.Evaluated;
            _variableExpander.UpdateDebugProperty(IsExpandable
                                                      ? DebugProperty3
                                                      : null);
        }

        DEBUG_PROPERTY_INFO PropertyInfo
        {
            get
            {
                EnsureVaribleHasBeenEvaluated();
                return _propertyInfo;
            }
        }

        IDebugProperty2 DebugProperty
        {
            get
            {
                EnsureVaribleHasBeenEvaluated();
                return _propertyInfo.pProperty;
            }
        }

        IDebugProperty3 DebugProperty3 => DebugProperty as IDebugProperty3;

        void EnsureVaribleHasBeenEvaluated()
        {
            if (State != VariableState.Evaluated)
            {
                throw new InvalidOperationException(
                    "Variable has not been evaluated under the selected stack frame " +
                    $"({_debugSessionContext.SelectedStackFrame}). " +
                    $"Current variable state = {State}.");
            }
        }
    }
}
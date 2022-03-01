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

namespace Google.VisualStudioFake.Internal.UI
{
    public interface IVariableExpander : IVariableDataSource
    {
        /// <summary>
        /// Sets the current |debugProperty| and triggers a refresh for all children. If the new
        /// child count is different from the old one, all children are deleted.
        /// </summary>
        void UpdateDebugProperty(IDebugProperty3 debugProperty);

        /// <summary>
        /// Deletes all children.
        /// </summary>
        void DeleteChildren();

        /// <summary>
        /// Resets all children.
        /// </summary>
        void ResetChildren();

        /// <summary>
        /// Returns |count| child variable entries for the debug property passed to
        /// UpdateDebugProperty() starting from index |offset|. If the same index is queried
        /// multiple times, the same instance is returned.
        /// If after a debugger step the total child count changes (e.g. after a
        /// vector::push_back() operation), all existing children are deleted.
        /// </summary>
        IList<IVariableEntry> GetOrCreateChildren(int offset, int count);
    }

    /// <summary>
    /// Expands debug properties, e.g. watch window entries. The class takes an IDebugProperty3
    /// instance, which represents the debug property to be expanded, gets an
    /// IEnumDebugPropertyInfo2 instance by calling EnumChildren and from that the
    /// GetOrCreateChildren() gets/creates the actual children as IVariableEntry instances.
    /// </summary>
    public class VariableExpander : IVariableExpander
    {
        public class Factory
        {
            readonly IDebugSessionContext _debugSessionContext;
            readonly int _batchSize;
            VariableEntry.Factory _variableEntryFactory;

            public Factory(IDebugSessionContext debugSessionContext, int batchSize)
            {
                _debugSessionContext = debugSessionContext;
                _batchSize = batchSize;
            }

            public void SetVariableEntryFactory(VariableEntry.Factory variableEntryFactory)
            {
                _variableEntryFactory = variableEntryFactory;
            }

            public virtual VariableExpander Create() =>
                new VariableExpander(_variableEntryFactory, _debugSessionContext, _batchSize);
        }

        readonly VariableEntry.Factory _variableEntryFactory;
        readonly IDebugSessionContext _debugSessionContext;
        readonly int _batchSize;
        readonly Dictionary<IVariableEntryInternal, int> _childToIndex;
        readonly Dictionary<int, IVariableEntryInternal> _indexToChild;

        IDebugProperty3 _debugProperty;
        IEnumDebugPropertyInfo2 _childEnum;

        // Buffer for requested DEBUG_PROPERTY_INFOs.
        Queue<DEBUG_PROPERTY_INFO> _currentBatch = new Queue<DEBUG_PROPERTY_INFO>();

        // Index of the next element in _currentBatch.
        int _currentBatchIndex;

        int _totalChildCount;

        /// <param name="batchSize">
        /// Number of children to request per IEnumDebugPropertyInfo2.Next() call. Visual Studio
        /// appears to use 10 for Watch, Locals and Autos windows, 15 for variable tooltips and
        /// int.MaxValue (all children in one batch) for the Registers window.
        /// </param>
        public VariableExpander(VariableEntry.Factory variableEntryFactory,
                                IDebugSessionContext debugSessionContext, int batchSize)
        {
            _variableEntryFactory = variableEntryFactory;
            _debugSessionContext = debugSessionContext;
            _batchSize = batchSize;
            _childToIndex = new Dictionary<IVariableEntryInternal, int>();
            _indexToChild = new Dictionary<int, IVariableEntryInternal>();
        }

        #region IVariableExpander

        public void UpdateDebugProperty(IDebugProperty3 debugProperty)
        {
            if (_debugProperty == debugProperty)
            {
                return;
            }
            _debugProperty = debugProperty;

            int prevTotalChildCount = _totalChildCount;
            if (_debugProperty != null)
            {
                Guid guidFilter = Guid.Empty;
                _childEnum = null;
                HResultChecker.Check(
                    debugProperty.EnumChildren(
                        enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ALL, Radix, ref guidFilter,
                        enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_NONE, null, 0, out _childEnum));

                uint totalChildCount;
                HResultChecker.Check(_childEnum.GetCount(out totalChildCount));
                _totalChildCount = (int)totalChildCount;
            }
            else
            {
                _childEnum = null;
                _totalChildCount = 0;
            }

            _currentBatch.Clear();
            _currentBatchIndex = 0;

            // Emulate Visual Studio:
            // If the number of children changed, collapse (delete) them. Otherwise, refresh them.
            if (prevTotalChildCount != _totalChildCount)
            {
                DeleteChildren();
            }
            else
            {
                foreach (var child in _childToIndex.Keys)
                {
                    child.Refresh();
                }
            }
        }

        public void DeleteChildren()
        {
            foreach (var child in _childToIndex.Keys)
            {
                child.OnDelete();
            }
            _childToIndex.Clear();
            _indexToChild.Clear();
        }

        public void ResetChildren()
        {
            foreach (var child in _childToIndex.Keys)
            {
                child.OnReset();
            }
        }

        public IList<IVariableEntry> GetOrCreateChildren(int offset, int count)
        {
            int beginIndex = Math.Max(0, offset);
            int endIndex = (int)Math.Min(offset + (long)count, _totalChildCount);

            List<IVariableEntry> children = new List<IVariableEntry>();
            for (int index = beginIndex; index < endIndex; ++index)
            {
                IVariableEntry child = GetOrCreateChild(index);
                children.Add(child);
            }
            return children;
        }

        #endregion

        #region IVariableDataSource

        public void GetCurrentState(IVariableEntryInternal child,
                                    Action<DEBUG_PROPERTY_INFO> callback)
        {
            int index = _childToIndex[child];
            if (index != _currentBatchIndex)
            {
                HResultChecker.Check(_childEnum.Reset());
                HResultChecker.Check(_childEnum.Skip((uint)index));
                _currentBatchIndex = index;
            }

            if (_currentBatch.Count == 0)
            {
                int toFetch =
                    Math.Max(0, Math.Min(_batchSize, _totalChildCount - _currentBatchIndex));
                var propertyInfos = new DEBUG_PROPERTY_INFO[toFetch];
                uint numFetched;
                HResultChecker.Check(
                    _childEnum.Next((uint)toFetch, propertyInfos, out numFetched));

                foreach (var info in propertyInfos)
                {
                    _currentBatch.Enqueue(info);
                }
            }

            _currentBatchIndex++;
            callback(_currentBatch.Dequeue());
        }

        #endregion

        IVariableEntry GetOrCreateChild(int index)
        {
            IVariableEntryInternal child;
            if (!_indexToChild.TryGetValue(index, out child))
            {
                child = _variableEntryFactory.Create(this);
                if (_debugSessionContext.ProgramState == ProgramState.AtBreak)
                {
                    child.Refresh();
                }

                _indexToChild[index] = child;
                _childToIndex[child] = index;
            }

            return child;
        }

        uint Radix => _debugSessionContext.HexadecimalDisplay ? 16u : 10u;
    }
}

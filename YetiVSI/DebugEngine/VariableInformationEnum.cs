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

﻿using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using YetiVSI.DebugEngine.Interfaces;

namespace YetiVSI.DebugEngine
{
    public interface IVariableInformationEnumFactory
    {
        IEnumDebugPropertyInfo2 Create(IChildrenProvider childrenProvider);
    }

    // Represents a collection of variables for display in a debug window. IVariableInformation
    // instances are converted to DEBUG_PROPERTY_INFO instances when a client calls the Next
    // method.
    public class VariableInformationEnum : IEnumDebugPropertyInfo2
    {
        public class Factory : IVariableInformationEnumFactory
        {
            readonly ITaskExecutor _taskExecutor;

            public Factory(ITaskExecutor taskExecutor)
            {
                _taskExecutor = taskExecutor;
            }

            public virtual IEnumDebugPropertyInfo2 Create(IChildrenProvider childrenProvider) =>
                new VariableInformationEnum(_taskExecutor, childrenProvider);
        }

        readonly ITaskExecutor _taskExecutor;
        readonly IChildrenProvider _childrenProvider;

        // Index of the child to start reading from when Next is called. It
        // is modified when reading, resetting, and skipping and ranges from 0 to maxChildren + 1.
        int _childOffset;

        VariableInformationEnum(ITaskExecutor taskExecutor,
                                IChildrenProvider childrenProvider)
        {
            _taskExecutor = taskExecutor;
            _childrenProvider = childrenProvider;

            _childOffset = 0;
        }

        #region IEnumDebugPropertyInfo2 functions

        public int Clone(out IEnumDebugPropertyInfo2 infoEnum)
        {
            infoEnum = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetCount(out uint count)
        {
            count = (uint) _taskExecutor.Run(async () =>
                                                 await _childrenProvider.GetChildrenCountAsync());
            return VSConstants.S_OK;
        }

        public int Next(uint count, DEBUG_PROPERTY_INFO[] outPropertyInfo, out uint numFetched)
        {
            numFetched = (uint) _taskExecutor.Run(async () =>
                                                      await _childrenProvider.GetChildrenAsync(
                                                          _childOffset, (int) count,
                                                          outPropertyInfo));

            _childOffset += (int) numFetched;
            return numFetched == count ? VSConstants.S_OK : VSConstants.S_FALSE;
        }

        public int Reset()
        {
            _childOffset = 0;
            return VSConstants.S_OK;
        }

        public int Skip(uint count)
        {
            _childOffset += (int) count;

            int maxOffset = _taskExecutor.Run(async () =>
                                                  await _childrenProvider.GetChildrenCountAsync());
            if (_childOffset > maxOffset)
            {
                _childOffset = maxOffset;
                return VSConstants.S_FALSE;
            }

            return VSConstants.S_OK;
        }

        #endregion
    }
}
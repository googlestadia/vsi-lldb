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
using System;
using System.Collections.Generic;
using System.Linq;
using YetiVSI.DebugEngine.AsyncOperations;

namespace YetiVSI.DebugEngine
{
    /// <summary>
    /// Holds current stack of frames and lazy-loads them.
    /// We are aware of two ways Visual Studio uses this implementation:
    /// 1. Loads all frames for 'Call Stack' window and for frames combobox. In this case it calls
    /// <see cref="IEnumDebugFrameInfo2.GetCount"/> and after that
    /// <see cref="IEnumDebugFrameInfo2.Next"/> with 'number' parameter equal to count fetched.
    /// 2. Loads one top frame. Calls <see cref="IEnumDebugFrameInfo2.Next"/>
    /// with 'number' parameter equal to 1. In this case we benefit from lazy-loading.
    /// </summary>
    public class FrameInfoEnum : IEnumDebugFrameInfo2
    {
        readonly StackFramesProvider _stackFramesProvider;
        readonly enum_FRAMEINFO_FLAGS _fieldSpec;
        readonly IDebugThread _debugThread;
        readonly List<FRAMEINFO> _data;

        uint _position;
        bool _allStacksLoaded;

        public FrameInfoEnum(StackFramesProvider stackFramesProvider,
            enum_FRAMEINFO_FLAGS fieldSpec, IDebugThread debugThread)
        {
            _data = new List<FRAMEINFO>();
            _stackFramesProvider = stackFramesProvider;
            _fieldSpec = fieldSpec;
            _debugThread = debugThread;
            _position = 0;
            _allStacksLoaded = false;
        }

        public int Next(uint number, FRAMEINFO[] results, ref uint numFetched)
        {
            uint prevPosition = _position;
            numFetched = PopulateNextItems(number);

            for(int i = 0; i < numFetched; ++i)
            {
                results[i] = _data[(int)prevPosition + i];
            }

            return numFetched < number ? VSConstants.S_FALSE : VSConstants.S_OK;
        }

        public int Skip(uint number)
        {
            uint loaded = PopulateNextItems(number);
            return loaded < number ? VSConstants.S_FALSE : VSConstants.S_OK;
        }

        public int Reset()
        {
            _position = 0;
            return VSConstants.S_OK;
        }

        public int Clone(out IEnumDebugFrameInfo2 ppEnum)
        {
            ppEnum = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetCount(out uint count)
        {
            /// When Visual Studio wants to know count of frames,
            /// it will load all of them anyway, so this is not redundant operation.
            /// This way though we don't need another special method to get only count of frames.
            bool success = LoadAll();
            count = (uint)_data.Count;
            return success ? VSConstants.S_OK : VSConstants.S_FALSE;
        }

        /// <summary>
        /// Returns true if all the available frames are loaded
        /// or when the maximum number of frames is loaded.
        /// </summary>
        bool EndOfStackReached =>
            _allStacksLoaded || _data.Count == _stackFramesProvider.MaxFramesNumberToLoad;

        /// <summary>
        /// Loads all frames.
        /// </summary>
        /// <returns>True if successful, otherwise false.</returns>
        bool LoadAll()
        {
            if (EndOfStackReached)
            {
                return true;
            }

            IList<FRAMEINFO> loadedFrames = _stackFramesProvider.GetRange(
                _fieldSpec, _debugThread, (uint)_data.Count, uint.MaxValue);

            if (loadedFrames == null)
            {
                return false;
            }

            _data.AddRange(loadedFrames);
            _allStacksLoaded = true;
            return true;
        }

        /// <summary>
        /// Tries to populate next frames up to _position + number.
        /// </summary>
        /// <param name="number">Number of frames to load.</param>
        /// <returns>Number of actually populated frames.</returns>
        uint PopulateNextItems(uint number)
        {
            if (_position + number <= _data.Count)
            {
                _position += number;
                return number;
            }

            if (EndOfStackReached)
            {
                // The end was reached, just return what we have.
                number = (uint)(_data.Count - _position);
                _position += number;
                return number;
            }

            /// We don't want to make lot's of small requests,
            /// that's why we load more frames when requested number is small.
            /// So we double size of data even if requested number is much smaller.
            uint minBatchSize = (uint)_data.Count;
            uint additionalCountNeeded = _position + number - (uint)_data.Count;
            uint countToLoad = Math.Max(additionalCountNeeded, minBatchSize);
            IList<FRAMEINFO> loadedFrames = _stackFramesProvider.GetRange(
                _fieldSpec, _debugThread, (uint)_data.Count, countToLoad);

            if (loadedFrames == null)
            {
                return 0;
            }

            _data.AddRange(loadedFrames);
            _allStacksLoaded = loadedFrames.Count < countToLoad;

            uint prevPosition = _position;
            _position = Math.Min(_position + number, (uint)_data.Count);

            return _position - prevPosition;
        }
    }
}

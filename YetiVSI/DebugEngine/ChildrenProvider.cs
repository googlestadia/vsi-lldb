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
using System.Threading.Tasks;
using Microsoft.VisualStudio.Debugger.Interop;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.DebugEngine
{
    public interface IChildrenProviderFactory
    {
        IChildrenProvider Create(IChildAdapter childAdapter,
                                 enum_DEBUGPROP_INFO_FLAGS debugInfoFlags, uint radix);
    }

    public interface IChildrenProvider
    {
        /// <summary>
        /// Return the number of children asynchronously.
        /// </summary>
        Task<int> GetChildrenCountAsync();

        /// <summary>
        /// Asynchronously provide debug property info for children elements. If the requested range
        /// exceeds the children count, all the children starting from fromIndex will be returned.
        /// </summary>
        /// <param name="fromIndex">Index of the first child to collect.</param>
        /// <param name="requestedCount">Number of children to collect.</param>
        /// <param name="outPropertyInfo">Array to be filled with children property info.</param>
        /// <returns>Awaitable task which returns the number of children collected.</returns>
        Task<int> GetChildrenAsync(int fromIndex, int requestedCount,
                                   DEBUG_PROPERTY_INFO[] outPropertyInfo);
    }

    public class ChildrenProvider : IChildrenProvider
    {
        public class Factory : IChildrenProviderFactory
        {
            IGgpDebugPropertyFactory _propertyFactory;

            /// <summary>
            /// DebugPropertyCreator is required as an additional dependency but can't be provided
            /// at construction time due to circular dependency. Use Initialize() to provide it.
            /// </summary>
            public void Initialize(IGgpDebugPropertyFactory propertyFactory)
            {
                _propertyFactory = propertyFactory;
            }

            public virtual IChildrenProvider Create(IChildAdapter childAdapter,
                                                    enum_DEBUGPROP_INFO_FLAGS debugInfoFlags,
                                                    uint radix)
            {
                if (_propertyFactory == null)
                {
                    throw new NullReferenceException(
                        $"{nameof(_propertyFactory)} has to be initialized.");
                }

                return new ChildrenProvider(_propertyFactory, childAdapter, debugInfoFlags,
                                            radix);
            }
        }

        readonly IGgpDebugPropertyFactory _propertyFactory;

        readonly IChildAdapter _childAdapter;
        readonly enum_DEBUGPROP_INFO_FLAGS _debugInfoFlags;

        readonly uint _radix;

        ChildrenProvider(IGgpDebugPropertyFactory propertyFactory, IChildAdapter childAdapter,
                         enum_DEBUGPROP_INFO_FLAGS debugInfoFlags, uint radix)
        {
            _propertyFactory = propertyFactory;
            _childAdapter = childAdapter;
            _debugInfoFlags = debugInfoFlags;
            _radix = radix;
        }

        public async Task<int> GetChildrenCountAsync() => await _childAdapter.CountChildrenAsync();

        public async Task<int> GetChildrenAsync(int fromIndex, int requestedCount,
                                                DEBUG_PROPERTY_INFO[] outPropertyInfo)
        {
            IList<IVariableInformation> children =
                await _childAdapter.GetChildrenAsync(fromIndex, requestedCount);
            List<IGgpDebugProperty> debugProperties = children
                .Select(_propertyFactory.Create).ToList();

            for (int i = 0; i < debugProperties.Count; i++)
            {
                outPropertyInfo[i] = await CreatePropertyInfoAsync(debugProperties[i]);
            }

            return debugProperties.Count;
        }

        async Task<DEBUG_PROPERTY_INFO> CreatePropertyInfoAsync(IGgpDebugProperty property)
        {
            // TODO: Handle potential error resulting from
            // getting a DEBUG_PROPERTY_INFO.
            var propertyInfo = new DEBUG_PROPERTY_INFO[1];
            await property.GetPropertyInfoAsync(_debugInfoFlags, _radix, 0, null, 0, propertyInfo);
            return propertyInfo[0];
        }
    }
}
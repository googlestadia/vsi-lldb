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

﻿using Castle.DynamicProxy;
using Google.VisualStudioFake.API;
using System;
using System.Reflection;

namespace Google.VisualStudioFake.Internal.ExecutionSyncPoint
{
    /// <summary>
    /// Used to decorate VSFake API methods to support automatic job execution.
    /// </summary>
    /// <remarks>
    /// Job's will not be executed if the SessionDebugManager is in SDMExecutionMode.MANUAL
    /// execution mode.
    /// </remarks>
    public class SyncPointAttribute : Attribute
    {
        public SyncPointAttribute(API.ExecutionSyncPoint syncPoint)
        {
            SyncPoint = syncPoint;
        }

        public API.ExecutionSyncPoint SyncPoint { get; set; }

        public VSFakeTimeout Timeout { get; set; } = VSFakeTimeout.Short;
    }

    /// <summary>
    /// Used to automatically process the JobQueue when returning from VSFake API methods.
    /// </summary>
    public class SyncPointInterceptor : IInterceptor
    {
        readonly VSFakeTimeoutSource _timeoutSource;

        ISessionDebugManager _sessionDebugManager;

        public SyncPointInterceptor(VSFakeTimeoutSource timeoutSource)
        {
            _timeoutSource = timeoutSource;
        }

        public void SetSessionDebugManager(ISessionDebugManager sessionDebugManager)
        {
            _sessionDebugManager = sessionDebugManager;
        }

        #region IInterceptor

        public void Intercept(IInvocation invocation)
        {
            invocation.Proceed();

            var methodInfo = invocation.Method;

            switch (SessionDebugManager.ExecutionMode)
            {
                case SDMExecutionMode.MANUAL:
                    return;
                case SDMExecutionMode.AUTO:
                    // Intentional no-op.
                    break;
            }

            var attribute = methodInfo.GetCustomAttribute(typeof(SyncPointAttribute), true)
                as SyncPointAttribute;
            if (attribute == null)
            {
                return;
            }
            var syncAttribute = (SyncPointAttribute)attribute;

            SessionDebugManager.RunUntil(attribute.SyncPoint,
                _timeoutSource[syncAttribute.Timeout]);
        }

#endregion

        ISessionDebugManager SessionDebugManager
        {
            get
            {
                if (_sessionDebugManager == null)
                {
                    throw new InvalidOperationException($"{nameof(_sessionDebugManager)} has " +
                        $"not been set. Call {nameof(SetSessionDebugManager)}.");
                }

                return _sessionDebugManager;
            }
        }
    }
}

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
using YetiCommon.PerformanceTracing;

namespace YetiCommon.MethodRecorder
{
    public class MetricsCollectionAspect : IInterceptor
    {
        readonly IMethodInvocationRecorder _methodInvocationRecorder;
        readonly ITimeSource _timeSource;
        readonly long _debugSessionStartTimestampUs;

        public MetricsCollectionAspect(
            IMethodInvocationRecorder methodInvocationRecorder, ITimeSource timeSource)
        {
            this._methodInvocationRecorder = methodInvocationRecorder;
            this._timeSource = timeSource;
            _debugSessionStartTimestampUs = _timeSource.GetTimestampUs();
        }

        public void Intercept(IInvocation invocation)
        {
            long initialTimestampInMicro =
                _timeSource.GetTimestampUs() - _debugSessionStartTimestampUs;
            try
            {
                invocation.Proceed();
            }
            finally
            {
                SafeErrorUtil.SafelyLogError(() =>
                {
                    long finalTimestampInMicro =
                        _timeSource.GetTimestampUs() - _debugSessionStartTimestampUs;
                    _methodInvocationRecorder.Record(invocation.MethodInvocationTarget,
                                                     initialTimestampInMicro,
                                                     finalTimestampInMicro);
                }, "Failed to record method invocation");
            }
        }
    }
}

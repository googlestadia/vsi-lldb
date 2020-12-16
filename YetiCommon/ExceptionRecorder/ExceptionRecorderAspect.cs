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
using System;

namespace YetiCommon.ExceptionRecorder
{
    public class ExceptionRecorderAspect : IInterceptor
    {
        readonly IExceptionRecorder exceptionRecorder;

        public ExceptionRecorderAspect(IExceptionRecorder exceptionRecorder)
        {
            if (exceptionRecorder == null)
            {
                throw new ArgumentNullException(nameof(exceptionRecorder));
            }
            this.exceptionRecorder = exceptionRecorder;
        }

        public void Intercept(IInvocation invocation)
        {
            try
            {
                invocation.Proceed();
            }
            catch (Exception ex)
            {
                SafeErrorUtil.SafelyLogError(() => {
                    exceptionRecorder.Record(invocation.MethodInvocationTarget, ex);
                }, "Failed to record exception");
                throw;
            }
        }
    }
}
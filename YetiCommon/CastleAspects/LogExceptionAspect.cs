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
using System.Diagnostics;
using System.Text;
using YetiCommon.Logging;

namespace YetiCommon.CastleAspects
{
    /// <summary>
    /// Logs details from an exception being raised from the decorated method.
    ///
    /// Note: OutOfMemoryExceptions will not be logged.
    /// </summary>
    public class LogExceptionAspect : IInterceptor
    {
        readonly string SECTION_SEPARATOR =
            "--------------------------------------------------------------";

        InvocationLogUtil logUtil;

        public LogExceptionAspect()
        {
            logUtil = new InvocationLogUtil();
        }

        #region IInterceptor

        public void Intercept(IInvocation invocation)
        {
            try
            {
                invocation.Proceed();
            }
            catch (Exception ex) when (SafeLog(ex, invocation) && false)
            {
                Debug.Fail("Exception should never be caught!");
                throw;
            }
        }

        #endregion

        private bool SafeLog(Exception exception, IInvocation invocation)
        {
            // Suppress all exceptions from this method.
            try
            {
                if (exception == null || invocation == null)
                {
                    Trace.WriteLine(
                        "ERROR: Failed to log unhandled exception. " +
                        "One or more arguments were null.");
                    return false;
                }

                var stringBuilder = new StringBuilder();

                // Write the default exception information.
                stringBuilder.AppendLine("ERROR: Unhandled Exception information");
                stringBuilder.AppendLine(SECTION_SEPARATOR);
                stringBuilder.AppendLine(exception.ToString());
                stringBuilder.AppendLine(SECTION_SEPARATOR);
                stringBuilder.AppendLine();

                stringBuilder.AppendLine(
                    "Additional context information (Invocation and parameter values)");
                stringBuilder.AppendLine(SECTION_SEPARATOR);
                stringBuilder.Append(SafeContextLog(invocation));
                stringBuilder.AppendLine(SECTION_SEPARATOR);

                Trace.WriteLine(stringBuilder);


            }
            catch { }
            return false;
        }

        private StringBuilder SafeContextLog(IInvocation invocation)
        {
            var stringBuilder = new StringBuilder();
            try
            {
                logUtil.AppendCallInformation(stringBuilder, invocation);
            }
            catch (Exception ex)
            {
                if (typeof(OutOfMemoryException) != ex.GetType())
                {
                    stringBuilder = new StringBuilder();
                    stringBuilder.AppendLine(
                        "WARNING: Failed to serialize invocation.  Reason: " + ex.Message);
                }
            }
            return stringBuilder;
        }
    }
}

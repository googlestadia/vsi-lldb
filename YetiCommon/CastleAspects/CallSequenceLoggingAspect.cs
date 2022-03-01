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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace YetiCommon.CastleAspects
{
    /// <summary>
    /// This aspect generates call sequence diagrams according to the calls
    /// performed by decorated objects. In order to visualize those, access
    /// (internal) and use the content of the <log file>.
    /// Please note that this is *not* thread-safe.
    /// </summary>
    public class CallSequenceLoggingAspect : IInterceptor
    {
        readonly ObjectIDGenerator idGenerator;
        readonly NLog.ILogger logger;
        readonly Stack<object> proxies;

        public CallSequenceLoggingAspect(ObjectIDGenerator idGenerator, NLog.ILogger logger)
        {
            this.logger = logger;
            this.idGenerator = idGenerator;
            proxies = new Stack<object>();

            // Force VS be the left most column in the rendered diagram.
            logger.Trace("# Participant Section");
            logger.Trace("participant VS");

            // Output an empty line.
            logger.Trace("");
            logger.Trace("# Call Section");
        }

        public void Intercept(IInvocation invocation)
        {
            object proxy = invocation.Proxy;
            string callerDesc = GetCallerDescription(out string callerMetaData);
            string proxyDesc = GetProxyDescription(proxy);

            string metaData = string.IsNullOrEmpty(callerMetaData) ? "" : $" [{callerMetaData}]";
            logger.Trace(
                $"{callerDesc} -> {proxyDesc}: {GetInvocationDescription(invocation)}" +
                metaData);
            proxies.Push(proxy);
            try
            {
                invocation.Proceed();
            }
            catch (Exception ex)
            {
                logger.Trace($"{proxyDesc} --> {callerDesc}: {ex.GetType()}");
                throw;
            }
            finally
            {
                proxies.Pop();
            }
            string retDesc = GetReturnDescription(invocation);
            if (!string.IsNullOrEmpty(retDesc))
            {
                logger.Trace($"{proxyDesc} --> {callerDesc}: {retDesc}");
            }
        }

        string GetInvocationDescription(IInvocation invocation)
        {
            MethodInfo info = invocation.Method;
            return $"{info.DeclaringType.Name}.{info.Name} (id={GetId(invocation.Proxy)})";
        }

        static IEnumerable<object> GetOutArguments(IInvocation invocation) =>
            invocation.Method.GetParameters().Zip(invocation.Arguments, (p, a) =>
            new Tuple<ParameterInfo, object>(p, a)).Where(t => t.Item1.IsOut).Select(t => t.Item2);

        // This may assume that caller == "VS", whereas it is actually a gap aspect coverage
        // across the interop boundary. See (internal) for more info.
        string GetCallerDescription(out string callerMetaData)
        {
            if (proxies.Count == 0)
            {
                callerMetaData = null;
                return "VS";
            }
            callerMetaData = "color=\"red\"";
            return GetProxyDescription(proxies.Peek());
        }

        string GetProxyDescription(object proxy)
        {
            FieldInfo targetField = proxy?.GetType().GetFields(
                BindingFlags.NonPublic | BindingFlags.Instance).Where(f => f.Name == "__target")
                .FirstOrDefault();
            if (targetField != null)
            {
                return $"{targetField.GetValue(proxy)} (id={GetId(proxy)})";
            }

            // We ignore the case when proxy is a string so we don't end up logging multiple chars.
            if (proxy is IEnumerable arr && !(arr is string))
            {
                string arrDesc = string.Join(", ", arr.Cast<object>()
                    .Select(a => GetProxyDescription(a))
                    .Where(desc => !string.IsNullOrEmpty(desc)));
                // '[' characters need to be escaped to be output as literal text.
                return $"\\[{arrDesc}]";
            }

            return proxy != null ? $"{{{proxy.GetType()}}} (id={GetId(proxy)})" : "";
        }

        /// <summary>
        /// Generates a description for an invocation.  Includes type info for out parameters and
        /// possibly values for 'simple' types.  The invocation must have already proceeded before
        /// the execution of this method.
        /// </summary>
        string GetReturnDescription(IInvocation invocation) =>
            string.Join(", ", GetOutArguments(invocation).Select(
                a => GetOutArgDescription(a)).Where(d => !string.IsNullOrEmpty(d)));

        string GetOutArgDescription(object arg)
        {
            string desc = GetProxyDescription(arg);
            if (IsSimpleType(arg))
            {
                desc += $" {{value={arg}}}";
            }
            return desc;
        }

        /// <summary>
        /// Determine if the type of an object is 'simple' and it's value should be logged.
        /// </summary>
        static bool IsSimpleType(object o)
        {
            if (o == null)
            {
                return false;
            }
            Type type = o.GetType();
            return type.IsPrimitive ||
                   type.IsEnum ||
                   type.Equals(typeof(string)) ||
                   type.Equals(typeof(decimal));
        }

        long GetId(object obj)
        {
            return obj == null ? 0 : idGenerator.GetId(obj, out _);
        }
    }
}

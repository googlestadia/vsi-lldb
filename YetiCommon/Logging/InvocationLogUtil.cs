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

﻿using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Specialized;
using System.Reflection;
using System.Text;

namespace YetiCommon.Logging
{
    /// <summary>
    /// Helper class to build log messages from an IInvocation.
    /// </summary>
    public class InvocationLogUtil
    {
        /// <summary>
        /// Builds a full log message based on a MethodInfo and the array of arguments.
        /// </summary>
        /// <param name="stringBuilder"></param>
        /// <param name="methodInfo"></param>
        /// <param name="args"></param>
        /// <exception cref="ArgumentNullException">Thrown if any argument is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown if methodInfo.GetParameters() and |args| don't have the same length.
        /// </exception>"
        public void AppendCallInformation(StringBuilder stringBuilder, MethodInfo methodInfo,
            object[] args)
        {
            if (stringBuilder == null) { throw new ArgumentNullException(nameof(stringBuilder)); }
            if (methodInfo == null) { throw new ArgumentNullException(nameof(methodInfo)); }
            if (args == null) { throw new ArgumentNullException(nameof(args)); }

            stringBuilder.AppendLine("Method Invocation Log: ");
            stringBuilder.AppendLine("Target Object Type: " + methodInfo.DeclaringType);
            stringBuilder.AppendLine("Method Signature: " + methodInfo.ToString());

            var arguments = args;
            stringBuilder.AppendLine("Args [ToString]:");
            AppendArgumentsToString(stringBuilder, methodInfo.GetParameters(), arguments);
            stringBuilder.AppendLine("Args [Json]:");
            AppendArgumentsJson(stringBuilder, methodInfo.GetParameters(), arguments);
        }

        /// <summary>
        /// Appends argument values in to a StringBuilder using ToString() on the arguments.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if any argument is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown if the |paraminfo| and |args| arrays aren't the same length.
        /// </exception>
        public void AppendArgumentsToString(StringBuilder stringBuilder,
            ParameterInfo[] parameterInfo, object[] args)
        {
            if (stringBuilder == null) { throw new ArgumentNullException(nameof(stringBuilder)); }
            if (parameterInfo == null) { throw new ArgumentNullException(nameof(parameterInfo)); }
            if (args == null) { throw new ArgumentNullException(nameof(args)); }
            if (parameterInfo.Length != args.Length)
            {
                throw new ArgumentException(
                    $"Expected {nameof(parameterInfo)}.Length == {nameof(args)}.Length but " +
                    $"found {parameterInfo.Length} != {args.Length}.");
            }

            var errorBuilder = new StringBuilder();

            stringBuilder.Append("(");
            for (var i = 0; i < args.Length; i++)
            {
                stringBuilder.AppendLine();

                var paramName = parameterInfo[i].Name;
                var paramValue = "<Unknown>";
                try
                {
                    paramValue = args[i] != null ? args[i].ToString() : "null";
                }
                catch (Exception ex)
                {
                    errorBuilder.AppendLine(
                        $"<Failed to serialize argument '{paramName}' at index={i}");
                    errorBuilder.AppendLine($"  Reason: {ex}");
                    errorBuilder.AppendLine(">");
                }
                stringBuilder.Append("  " + paramName + "=" + paramValue);

                if (i < args.Length - 1)
                {
                    stringBuilder.Append(",");
                }
            }
            if (args.Length > 0)
            {
                // Output "()" if there are no args.
                stringBuilder.AppendLine();
            }
            stringBuilder.AppendLine(")");

            if (errorBuilder.Length > 0)
            {
                stringBuilder.AppendLine("----- Argument Serialization Errors <START> ----- ");
                stringBuilder.Append(errorBuilder);
                stringBuilder.AppendLine("----- Argument Serialization Errors <END> ----- ");
            }
        }

        /// <summary>
        /// Appends argument values in to a StringBuilder in Json format.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if any argument is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown if the |paraminfo| and |args| arrays aren't the same length.
        /// </exception>
        public void AppendArgumentsJson(StringBuilder stringBuilder, ParameterInfo[] parameterInfo,
            object[] args)
        {
            if (stringBuilder == null) { throw new ArgumentNullException(nameof(stringBuilder)); }
            if (parameterInfo == null) { throw new ArgumentNullException(nameof(parameterInfo)); }
            if (args == null) { throw new ArgumentNullException(nameof(args)); }
            if (parameterInfo.Length != args.Length)
            {
                throw new ArgumentException(
                    $"Expected {nameof(parameterInfo)}.Length == {nameof(args)}.Length but " +
                    $"found {parameterInfo.Length} != {args.Length}.");
            }

            var errorBuilder = new StringBuilder();

            var serializeSettings = new JsonSerializerSettings();
            serializeSettings.Formatting = Formatting.Indented;
            // Ensure we don't serialize extremely deep object graphs.
            serializeSettings.MaxDepth = 5;
            serializeSettings.NullValueHandling = NullValueHandling.Include;
            serializeSettings.ReferenceLoopHandling = ReferenceLoopHandling.Error;
            serializeSettings.Error += delegate (object sender, ErrorEventArgs jsonErrorArgs)
            {
                jsonErrorArgs.ErrorContext.Handled = true;

                errorBuilder.AppendLine(
                    $"<Failed to serialize '{jsonErrorArgs.ErrorContext.Member}'.");
                errorBuilder.AppendLine($"  Reason: {jsonErrorArgs.ErrorContext.Error}");
                errorBuilder.AppendLine(">");
            };

            try
            {
                var paramMap = new OrderedDictionary();
                for (var i = 0; i < args.Length; i++)
                {
                    paramMap.Add(parameterInfo[i].Name, args[i]);
                }
                stringBuilder.Append("(");
                if (args.Length > 0)
                {
                    // Output "()" if there are no args.
                    stringBuilder.AppendLine();
                    stringBuilder.Append(
                        JsonConvert.SerializeObject(paramMap, serializeSettings));
                    stringBuilder.AppendLine();
                }
                stringBuilder.AppendLine(")");
            }
            catch (System.IO.FileNotFoundException ex)
            {
                // TODO: Json serialization is failing occasionally due to missing
                // assembly. Don't want to break normal functionality in this case.
                stringBuilder.AppendLine("...<ERROR>...");
                errorBuilder.AppendLine($"<Failed to serialize. Reason: {ex}>");
            }

            if (errorBuilder.Length > 0)
            {
                stringBuilder.AppendLine("----- Argument Serialization Errors <START> ----- ");
                stringBuilder.Append(errorBuilder);
                stringBuilder.AppendLine("----- Argument Serialization Errors <END> ----- ");
            }
        }
    }
}
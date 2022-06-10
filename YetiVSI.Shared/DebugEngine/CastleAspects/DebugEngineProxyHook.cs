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
using System.Reflection;
using System.Diagnostics;
using Microsoft.VisualStudio.Debugger.Interop;
using System.Collections.Generic;
using System.Linq;

namespace YetiVSI.DebugEngine.CastleAspects
{
    /// <summary>
    /// The hook that defines the superset of methods that should be intercepted on debug engine
    /// domain objects.
    /// </summary>
    /// <remarks>
    /// This hook white lists all methods in the Microsoft.VisualStudio.Debugger.Interop namespace
    /// that do not have a ref arg.  TODO tracks the effort to support methods with ref
    /// args.
    /// Methods explicitly marked with the InteropBoundaryAttribute will also be intercepted.
    /// </remarks>
    public class DebugEngineProxyHook : YetiCommon.CastleAspects.ProxyHookBase
    {
        /// <summary>
        /// Helper class that checks if methods are declared within a specific namespace.
        /// </summary>
        class NamespaceScope
        {
            readonly string @namespace;

            public NamespaceScope(string @namespace)
            {
                this.@namespace = @namespace;
            }

            /// <summary>
            /// Determine if a method was declared in this namespace.
            /// </summary>
            /// <param name="type">The type that defines the target method.</param>
            /// <param name="methodInfo">The target method.</param>
            /// <returns>
            /// true if |methodInfo| was declared in this namespace.
            /// </returns>
            /// <exception cref="ArgumentNullException">Thrown if any argument is null.</exception>
            public bool DoesDeclareMethod(Type type, MethodInfo methodInfo)
            {
                if (type == null) { throw new ArgumentNullException(nameof(type)); }
                if (methodInfo == null) { throw new ArgumentNullException(nameof(methodInfo)); }

                if (type.IsInterface)
                {
                    return GetInterfacesInNamespace(type).Any(
                        interf => interf.GetMethods().Contains(methodInfo));
                }
                else if (type.IsClass)
                {
                    return GetInterfacesInNamespace(type).Any(
                        interf => type.GetInterfaceMap(interf).TargetMethods.Contains(methodInfo));
                }

                return false;
            }

            /// <summary>
            /// Returns true if the type was declared in this namespace.
            /// </summary>
            public bool DoesDeclareType(Type type)
            {
                return type.Namespace.StartsWith(@namespace);
            }

            /// <summary>
            /// Returns a set of interfaces implemented or inherited by a given type within this
            /// namespace. 'type' will also be included if it was declared in this namespace.
            /// </summary>
            private IEnumerable<Type> GetInterfacesInNamespace(Type type)
            {
                var declaredInterfaces = type.FindInterfaces(
                    new TypeFilter((t, o) => { return DoesDeclareType(t); }), null).ToList();
                if (DoesDeclareType(type))
                {
                    declaredInterfaces.Add(type);
                }
                return declaredInterfaces;
            }
        }

        readonly NamespaceScope interopNamespace;

        public DebugEngineProxyHook() : this(typeof(IDebugEngine2).Namespace) { }

        private DebugEngineProxyHook(string @namespace)
        {
            interopNamespace = new NamespaceScope(@namespace);
        }

        public static DebugEngineProxyHook CreateForTest(string @namespace)
        {
            return new DebugEngineProxyHook(@namespace);
        }

        #region IProxyGenerationHook

        public override bool ShouldInterceptMethod(Type type, MethodInfo methodInfo)
        {
            if (interopNamespace.DoesDeclareMethod(type, methodInfo) ||
                methodInfo.GetCustomAttribute(typeof(InteropBoundaryAttribute), true) != null)
            {
                if (HasRefArg(methodInfo))
                {
                    Debug.WriteLine(
                           $"Skipping Interop method: {methodInfo.DeclaringType}.{methodInfo}");
                    return false;
                }
                return true;
            }
            return false;
        }

        #endregion

        /// <summary>
        /// Checks if a method has a 'ref' argument.
        /// </summary>
        /// <param name="methodInfo">The method to inspect.</param>
        /// <returns>True when methodInfo has a 'ref' argument.</returns>
        private static bool HasRefArg(MethodInfo methodInfo)
        {
            foreach (var paramInfo in methodInfo.GetParameters())
            {
                if (paramInfo.ParameterType.IsByRef && !paramInfo.IsOut)
                {
                    return true;
                }
            }
            return false;
        }
    }
}

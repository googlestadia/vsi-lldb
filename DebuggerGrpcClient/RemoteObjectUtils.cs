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
using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace DebuggerGrpcClient
{
    public class RemoteObjectUtils
    {
        /// <summary>
        /// Creates a list of objects based on the provided handles guaranteeing that each handle
        /// will either be wrapped in an object or passed to a delete action.
        /// </summary>
        /// <param name="create">A function that creates an object given a handle.
        /// This function may not return null.</param>
        /// <param name="delete">A delete action that must be called when the object creation
        /// does not succeed.</param>
        /// <param name="handles">A list of remote handles.</param>
        /// <exception cref="InvalidOperationException">If create returns null.</exception>
        /// <returns>A list containing a remote object for each non-null handle and null
        /// for the null ones.</returns>
        public static List<TRemote> CreateRemoteObjects<TRemote, THandle>(
            Func<THandle, TRemote> create, Action<THandle> delete, IEnumerable<THandle> handles)
            where TRemote : class
        {
            var remoteObjects = new List<TRemote>();
            Exception error = null;
            foreach (var handle in handles)
            {
                if (handle == null)
                {
                    remoteObjects.Add(null);
                    continue;
                }

                if (error == null)
                {
                    try
                    {
                        var remoteObject = create(handle);
                        if (remoteObject == null)
                        {
                            throw new InvalidOperationException(
                                $"create returned null for handle ({handle})");
                        }
                        remoteObjects.Add(remoteObject);
                    }
                    catch (Exception e)
                    {
                        error = e;
                    }
                }
                // Please note that this is intentionally not an if-else; both blocks of code must
                // be executed for the handle which sets the error.
                if (error != null)
                {
                    try
                    {
                        delete(handle);
                    }
                    catch (Exception e)
                    {
                        Trace.WriteLine(
                            $"Warning: Could not delete handle ({handle}): {e.Message}");
                    }
                }
            }
            if (error != null)
            {
                ExceptionDispatchInfo.Capture(error).Throw();
            }
            return remoteObjects;
        }
    }
}

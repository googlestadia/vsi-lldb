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

using System;
using System.Diagnostics;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace YetiVSI
{
    public class ServiceManager
    {
        /// <summary>
        /// Returns the global |serviceType| instance.
        /// </summary>
        public virtual object GetGlobalService(Type serviceType)
        {
            return Package.GetGlobalService(serviceType);
        }

        /// <summary>
        /// Ensures |serviceType| have been initialized before returning it.
        /// </summary>
        virtual public object RequireGlobalService(Type serviceType)
        {
            var service = GetGlobalService(serviceType);
            if (service == null)
            {
                Trace.WriteLine($"Unable to find {serviceType} service");
                throw new Exception($"Unable to find {serviceType} service");
            }
            return service;
        }

        /// <summary>
        /// Returns the global JoinableTaskContext.
        /// </summary>
        virtual public JoinableTaskContext GetJoinableTaskContext()
        {
            return ThreadHelper.JoinableTaskContext;
        }
    }
}

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

﻿using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Debugger.Interop;
using YetiVSI;
using YetiVSI.LLDBShell;
using YetiVSI.LoadSymbols;
using YetiVSI.Shared.Metrics;

namespace YetiVSITestsCommon.Services
{
    public class ServiceManagerStub : ServiceManager, IServiceProvider
    {
        readonly IDictionary<Type, object> _services;

        public ServiceManagerStub(IMetrics metrics, SLLDBShell lldbShell,
                                  YetiVSIService yetiVsiService, SVsOutputWindow outputWindow,
                                  IVsDebuggerSymbolSettingsManager120A symbolSettingsManager,
                                  ISessionNotifier sessionNotifier = null)
        {
            _services = new Dictionary<Type, object>()
            {
                { typeof(YetiVSIService), yetiVsiService },
                { typeof(SLLDBShell), lldbShell },
                { typeof(SMetrics), metrics },
                { typeof(SVsOutputWindow), outputWindow },
                { typeof(SVsShellDebugger), symbolSettingsManager },
                { typeof(SSessionNotifier), sessionNotifier },
            };
        }

        public override object GetGlobalService(Type serviceType)
        {
            return GetService(serviceType);
        }

        public override object RequireGlobalService(Type serviceType)
        {
            var service = GetService(serviceType);
            if (service == null)
            {
                throw new Exception($"Service not found: {serviceType}");
            }

            return service;
        }

        public object GetService(Type serviceType)
        {
            if (!_services.TryGetValue(serviceType, out object service))
            {
                return null;
            }

            return service;
        }
    }
}
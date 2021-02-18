// Copyright 2021 Google LLC
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
using Microsoft.VisualStudio.Threading;
using YetiVSI;

namespace YetiVSITestsCommon
{
    public class FakeServiceManager : ServiceManager
    {
        readonly IDictionary<Type, object> _services;
        readonly JoinableTaskContext _taskContext;

        public FakeServiceManager(JoinableTaskContext taskContext)
        {
            _taskContext = taskContext;
            _services = new Dictionary<Type, object>();
        }

        public override object GetGlobalService(Type serviceType) => GetService(serviceType);

        public override object RequireGlobalService(Type serviceType)
        {
            object service = GetService(serviceType);
            if (service == null)
            {
                throw new Exception($"Service not found: {serviceType}");
            }

            return service;
        }

        public override JoinableTaskContext GetJoinableTaskContext() => _taskContext;

        public object GetService(Type serviceType) =>
            _services.TryGetValue(serviceType, out object service)
                ? service
                : null;

        public void AddService(Type serviceType, object service)
        {
            _services.Add(serviceType, service);
        }
    }
}
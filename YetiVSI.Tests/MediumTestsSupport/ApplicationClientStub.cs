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

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GgpGrpc.Cloud;
using GgpGrpc.Models;

namespace YetiVSI.Test.MediumTestsSupport
{
    public class ApplicationClientStub : IApplicationClient
    {
        readonly List<Application> _applications;

        public class ApplicationClientFakeFactory : IApplicationClientFactory
        {
            public IApplicationClient Create(ICloudRunner runner) => new ApplicationClientStub(
                new List<Application>()
                {
                    new Application { Name = "Yeti Development Application", Id = "yeti-dev-app" }
                });
        }

        ApplicationClientStub(List<Application> applications)
        {
            _applications = applications;
        }

        public Task<Application> LoadByNameOrIdAsync(string value) =>
            Task.FromResult(_applications.Find(a => a.Name == value || a.Id == value));

        public Task<Application> GetApplicationAsync(string applicationId) =>
            throw new NotImplementedException();
    }
}
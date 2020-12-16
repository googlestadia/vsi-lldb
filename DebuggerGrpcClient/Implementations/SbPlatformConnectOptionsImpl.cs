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

using DebuggerApi;

namespace DebuggerGrpcClient
{
    // Creates SBPlatformConnectOptions objects.
    public class GrpcPlatformConnectOptionsFactory
    {
        public SbPlatformConnectOptions Create(string url)
        {
            return new SbPlatformConnectOptionsImpl(url);
        }
    }

    // Implementation of the SBPlatformConnectOptions interface that uses GRPC to make RPCs to a
    // remote endpoint.
    class SbPlatformConnectOptionsImpl : SbPlatformConnectOptions
    {
        readonly string url;

        internal SbPlatformConnectOptionsImpl(string url)
        {
            this.url = url;
        }

        #region SbPlatformConnectOptions

        public string GetUrl()
        {
            return url;
        }

        #endregion
    }
}

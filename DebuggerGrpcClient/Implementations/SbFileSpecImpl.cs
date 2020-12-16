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
using Debugger.Common;
using DebuggerApi;

namespace DebuggerGrpcClient
{
    // Creates SBFileSpec objects.
    class GrpcFileSpecFactory
    {
        public SbFileSpec Create(GrpcSbFileSpec grpcSbFileSpec)
        {
            if (grpcSbFileSpec == null) { throw new ArgumentNullException(nameof(grpcSbFileSpec));}
            return new SbFileSpecImpl(grpcSbFileSpec);
        }
    }

    // Implementation of the SBFileSpec interface that uses GRPC to make RPCs to a remote endpoint.
    class SbFileSpecImpl : SbFileSpec
    {
        readonly GrpcSbFileSpec grpcSbFileSpec;

        internal SbFileSpecImpl(GrpcSbFileSpec grpcSbFileSpec)
        {
            this.grpcSbFileSpec = grpcSbFileSpec;
        }

        #region SbFileSpec

        public string GetFilename()
        {
            return grpcSbFileSpec.Filename;
        }

        public string GetDirectory()
        {
            return grpcSbFileSpec.Directory;
        }

        #endregion
    }
}

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
    // Creates SBError objects.
    class GrpcErrorFactory
    {
        public SbError Create(GrpcSbError grpcSbError)
        {
            if (grpcSbError == null) { throw new ArgumentNullException(nameof(grpcSbError));}

            return new SbErrorImpl(grpcSbError);
        }
    }

    // Implementation of the SBError interface that uses GRPC to make RPCs to a remote endpoint.
    class SbErrorImpl : SbError
    {
        readonly GrpcSbError grpcSbError;

        internal SbErrorImpl(GrpcSbError grpcSbError)
        {
            this.grpcSbError = grpcSbError;
        }

        #region SbError

        public bool Fail()
        {
            return !grpcSbError.Success;
        }

        public uint GetError()
        {
            return grpcSbError.Code;
        }

        public string GetCString()
        {
            return grpcSbError.Error;
        }

        public bool Success()
        {
            return grpcSbError.Success;
        }

        #endregion
    }
}
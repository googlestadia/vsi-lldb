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

namespace YetiVSI.Test.TestSupport
{
    // Stub test double for an SbError.
    public class SbErrorStub : SbError
    {
        private readonly bool success;
        private readonly uint errCode;
        private readonly string cString;

        public SbErrorStub(bool success, string cString = null, uint errCode = 0)
        {
            this.success = success;
            this.errCode = errCode;
            this.cString = cString;

            // TODO: `Fail()` should actually be just `errCode != 0`. For now just make sure
            // `errCode` is always non-zero if `success` is false.
            if (!this.success && this.errCode == 0)
            {
                this.errCode = uint.MaxValue;
            }
        }

        public bool Fail()
        {
            return !success;
        }

        public uint GetError()
        {
            return errCode;
        }

        public string GetCString()
        {
            return cString;
        }

        public bool Success()
        {
            return success;
        }
    }
}

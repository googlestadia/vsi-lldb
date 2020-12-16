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
using Microsoft.VisualStudio;

namespace Google.VisualStudioFake.Internal
{
    /// <summary>
    /// This class is used to verify success of method calls that return an HResult. On non-success
    /// an exception is thrown and the logs are written to the test error writer.
    /// </summary>
    class HResultChecker
    {
        /// <summary>
        /// Ensure the hresult is success.
        /// </summary>
        /// <exception cref="InvalidHResultException">Thrown on non-success result code</exception>
        /// <param name="hresult"></param>
        public static void Check(int hresult)
        {
            if (hresult != VSConstants.S_OK)
            {
                throw new InvalidHResultException(hresult);
            }
        }

        public class InvalidHResultException : Exception
        {
            public InvalidHResultException(int hresult)
            {
                HResultValue = hresult;
            }

            public int HResultValue { get; }
        }
    }
}

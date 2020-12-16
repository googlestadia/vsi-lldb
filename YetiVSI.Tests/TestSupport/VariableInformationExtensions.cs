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

using System.Linq;
using System.Threading.Tasks;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.Test.TestSupport
{
    /// <summary>
    /// Extensions methods to insulate tests from changes to IVariableInformation API changes.
    /// </summary>
    static class VariableInformationExtensions
    {
        public static async Task<IVariableInformation[]> GetAllChildrenAsync(
            this IVariableInformation varInfo)
        {
            IChildAdapter childAdapter = varInfo.GetChildAdapter();
            return (await childAdapter.GetChildrenAsync(0, await childAdapter.CountChildrenAsync()))
                .ToArray();
        }
    }
}
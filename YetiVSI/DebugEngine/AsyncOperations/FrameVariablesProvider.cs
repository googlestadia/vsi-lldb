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

﻿using DebuggerApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.DebugEngine.AsyncOperations
{
    public class FrameVariablesProvider
    {
        public class PropertyFilterGuids
        {
            public static readonly Guid Registers =
                new Guid("223ae797-bd09-4f28-8241-2763bdc5f713");
            public static readonly Guid Locals = new Guid("b200f725-e725-4c53-b36a-1ec27aef12ef");
            public static readonly Guid AllLocals =
                new Guid("196db21f-5f22-45a9-b5a3-32cddb30db06");
            public static readonly Guid Arguments =
                new Guid("804bccea-0475-4ae7-8a46-1862688ab863");
            public static readonly Guid LocalsPlusArguments =
                new Guid("e74721bb-10c0-40f5-807f-920d37f95419");
            public static readonly Guid AllLocalsPlusArguments =
                new Guid("939729a8-4cb0-4647-9831-7ff465240d5f");
        }

        readonly IRegisterSetsBuilder _registerSetsBuilder;
        readonly RemoteFrame _lldbFrame;
        readonly IVariableInformationFactory _varInfoFactory;

        public FrameVariablesProvider(
            IRegisterSetsBuilder registerSetsBuilder,
            RemoteFrame lldbFrame,
            IVariableInformationFactory varInfoFactory)
        {
            _registerSetsBuilder = registerSetsBuilder;
            _lldbFrame = lldbFrame;
            _varInfoFactory = varInfoFactory;
        }

        public virtual ICollection<IVariableInformation> Get(Guid guidFilter)
        {
            IEnumerable<IVariableInformation> varInfosEnum = null;
            if (guidFilter == PropertyFilterGuids.Registers)
            {
                varInfosEnum = _registerSetsBuilder.BuildSets();
            }
            else if (guidFilter == PropertyFilterGuids.Locals ||
                guidFilter == PropertyFilterGuids.AllLocals)
            {
                varInfosEnum = _lldbFrame.GetVariables(false, true, false, true)
                    .Select(CreateVarInfo);
            }
            else if (guidFilter == PropertyFilterGuids.Arguments)
            {
                varInfosEnum = _lldbFrame.GetVariables(true, false, false, true)
                    .Select(CreateVarInfo);
            }
            else if (guidFilter == PropertyFilterGuids.LocalsPlusArguments ||
                guidFilter == PropertyFilterGuids.AllLocalsPlusArguments)
            {
                varInfosEnum = _lldbFrame.GetVariables(true, true, false, true)
                    .Select(CreateVarInfo);
            }
            return varInfosEnum?.ToList();
        }

        IVariableInformation CreateVarInfo(RemoteValue remoteValue) =>
            _varInfoFactory.Create(remoteValue);
    }
}

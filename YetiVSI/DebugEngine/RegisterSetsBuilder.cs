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
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.DebugEngine
{
    public interface IRegisterSetsBuilder
    {
        IEnumerable<IVariableInformation> BuildSets();
    }

    public class RegisterSetsBuilder : IRegisterSetsBuilder
    {
        private const string floatingPointRegistersSetName = "Floating Point Registers";

        public class Factory
        {
            private readonly IVariableInformationFactory varInfoFactory;

            [Obsolete("This constructor only exists to support mocking libraries.", error: true)]
            protected Factory() { }

            public Factory(IVariableInformationFactory varInfoFactory)
            {
                this.varInfoFactory = varInfoFactory;
            }

            public virtual IRegisterSetsBuilder Create(RemoteFrame frame) =>
                new RegisterSetsBuilder(varInfoFactory, frame);
        }

        private readonly IVariableInformationFactory varInfoFactory;
        private readonly RemoteFrame frame;

        private RegisterSetsBuilder(IVariableInformationFactory varInfoFactory, RemoteFrame frame)
        {
            this.varInfoFactory = varInfoFactory;
            this.frame = frame;
        }

        public IEnumerable<IVariableInformation> BuildSets() =>
            BuildRealSets().Concat(BuildSyntheticSets());

        private IEnumerable<IVariableInformation> BuildRealSets()
        {
            var varInfos = new List<IVariableInformation>();
            // Registers being included in more than one set must use different handles.
            // New handles can be obtained by calling frame.GetRegisters().
            var values = frame.GetRegisters();
            bool firstGroup = true;
            foreach (var value in values)
            {
                // Change the display name for the first group of registers to "CPU".
                // Visual Studio will display the "CPU" register group by default.
                var newName = firstGroup ? "CPU" : value.GetName();
                firstGroup = false;
                varInfos.Add(varInfoFactory.Create(value, newName));
            }
            return varInfos;
        }

        private IEnumerable<IVariableInformation> BuildSyntheticSets()
        {
            RemoteValue floatingPointRegs = frame.GetRegisters()
                .Where(r => r.GetName() == floatingPointRegistersSetName)
                .FirstOrDefault();
            if (floatingPointRegs == null)
            {
                return Enumerable.Empty<IVariableInformation>();
            }

            var sets = new List<IVariableInformation>();

            // A preset Natvis entry for CustomVisualizer.SSE shows the registers xmm<0-7>
            // contained in the floating point register set as VectorOfFloat32.
            sets.Add(varInfoFactory.Create(floatingPointRegs, "SSE",
                                           customVisualizer: CustomVisualizer.SSE));

            // A preset Natvis entry for CustomVisualizer.SSE2 shows the registers xmm<0-15>
            // contained in the floating point register set as VectorOfFloat64, and xmm<8-15> as
            // VectorOfFloat32.
            sets.Add(varInfoFactory.Create(floatingPointRegs, "SSE2",
                                           customVisualizer: CustomVisualizer.SSE2));

            return sets;
        }
    }
}

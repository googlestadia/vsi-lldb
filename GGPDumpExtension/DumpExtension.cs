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

﻿using Microsoft.VisualStudio.Debugger.VsDebugPresentationExtension;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace GgpDumpExtension
{
    [Export(typeof(IDumpExtension))]
    public class DumpExtension : IDumpExtension
    {
        public IEnumerable<EngineProvider> Engines => new []
        {
            // This must match the Guid for the Debug Engine in the pkgdef.
            new EngineProvider(new Guid("6e5c4e9a-119b-4cbf-8c39-24f304d34655"),
                "GGP Debug Engine")
        };

        public bool SupportDumpFileFormat(string fileName)
        {
            string lowerFileName = fileName?.ToLowerInvariant();
            return !string.IsNullOrEmpty(lowerFileName) &&
                   (lowerFileName.EndsWith(".dmp") || lowerFileName.EndsWith(".core"));
        }
    }
}

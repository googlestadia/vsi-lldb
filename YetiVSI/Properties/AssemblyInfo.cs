﻿// Copyright 2020 Google LLC
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

using Microsoft.VisualStudio.Shell;
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("YetiVSI")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("YetiVSI")]
[assembly: AssemblyCopyright("")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("1.60.0.0")]
[assembly: AssemblyFileVersion("1.60.0.0")]

[assembly: ProvideBindingRedirection(AssemblyName = "System.Buffers", NewVersion = "4.0.3.0", OldVersionLowerBound = "0.0.0.0", OldVersionUpperBound = "4.0.3.0")]
[assembly: ProvideBindingRedirection(AssemblyName = "System.Numerics.Vectors", NewVersion = "4.1.4.0", OldVersionLowerBound = "0.0.0.0", OldVersionUpperBound = "4.1.4.0")]
[assembly: ProvideBindingRedirection(AssemblyName = "System.Runtime.CompilerServices.Unsafe", NewVersion = "4.0.6.0", OldVersionLowerBound = "0.0.0.0", OldVersionUpperBound = "4.0.6.0")]

// Visual Studio has it's own Newtonsoft.Json and it can have a lower version than we need.
// We bundle our specific version, because there are dependencies that need it. This is not
// recommended, but technically possible:
// https://devblogs.microsoft.com/visualstudio/using-newtonsoft-json-in-a-visual-studio-extension/
[assembly: ProvideCodeBase(AssemblyName = "Newtonsoft.Json")]
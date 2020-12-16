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
[assembly: AssemblyVersion("1.57.0.0")]
[assembly: AssemblyFileVersion("1.57.0.0")]

[assembly: ProvideBindingRedirection(AssemblyName = "Newtonsoft.Json", NewVersion = "9.0.0.0", OldVersionLowerBound = "0.0.0.0", OldVersionUpperBound = "9.0.0.0")]
[assembly: ProvideBindingRedirection(AssemblyName = "Google.Apis.Auth", NewVersion = "1.25.0.0", OldVersionLowerBound = "0.0.0.0", OldVersionUpperBound = "1.25.0.0")]
[assembly: ProvideBindingRedirection(AssemblyName = "Google.Apis.Core", NewVersion = "1.25.0.0", OldVersionLowerBound = "0.0.0.0", OldVersionUpperBound = "1.25.0.0")]
// Copyright 2021 Google LLC
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

using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Threading;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using YetiVSI.ProjectSystem.Abstractions;

namespace YetiVSI
{
    public static class ProjectSystemLoader
    {
        class AssemblyResolve : IDisposable
        {
            public AssemblyResolve()
            {
                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            }

            public void Dispose()
            {
                AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
            }

            private static Assembly CurrentDomain_AssemblyResolve(object sender,
                                                                  ResolveEventArgs args)
            {
                // Ignore missing resources.
                if (args.Name.Contains(".resources"))
                    return null;

                // Check for assemblies already loaded.
                Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(
                    a => a.FullName == args.Name);
                if (assembly != null)
                    return assembly;

                // All dependencies should actually be already loaded, but just in case try loading
                // from the extension directory.
                var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var path = Path.Combine(dir, args.Name);
                if (File.Exists(path))
                    return Assembly.LoadFile(path);

                // Otherwise just ignore it, maybe it's a dependency we don't actually need.
                return null;
            }
        }

        private static readonly Version version_15 = new Version(15, 0);
        private static readonly Version version_16 = new Version(16, 0);

        private static Assembly LoadAssemblyFor(Version version)
        {
            string assemblyName;
            if (version >= version_16)
            {
                assemblyName = "Embedded/YetiVSI.ProjectSystem.v16.dll";
            }
            else if (version >= version_15)
            {
                assemblyName = "Embedded/YetiVSI.ProjectSystem.v15.dll";
            }
            else
            {
                throw new InvalidOperationException(
                    $"Can't find a proxy assembly for '{version}'. Maybe the version is too low?");
            }

            var executingAssembly = Assembly.GetExecutingAssembly();
            using (var stream = executingAssembly.GetManifestResourceStream(assemblyName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException(
                        $"There is no embedded resource with name '{assemblyName}'");
                }

                // Load the assembly from memory.
                var buffer = new byte[stream.Length];
                stream.Read(buffer, 0, buffer.Length);
                return Assembly.Load(buffer);
            }
        }

        private static T LoadImpl<T>(Version version, params object[] args)
        {
            var assembly = LoadAssemblyFor(version);

            using (new AssemblyResolve())
            {
                var type = assembly.GetExportedTypes().FirstOrDefault(
                    t => t.GetInterface(typeof(T).Name) != null);
                if (type == null)
                {
                    throw new InvalidOperationException(
                        $"Assembly {assembly} doesn't have a type implementing {typeof(T)}");
                }
                return (T)Activator.CreateInstance(type, args);
            }
        }

        public static IAsyncProject CreateAsyncProject(ConfiguredProject configuredProject)
        {
            var version = configuredProject.GetType().Assembly.GetName().Version;
            return LoadImpl<IAsyncProject>(version, configuredProject);
        }
    }
}

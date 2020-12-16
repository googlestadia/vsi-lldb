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
using YetiCommon.VSProject;

namespace Google.VisualStudioFake.API
{
    /// <summary>
    /// Contains methods for loading and building a sample project, and an accessor to its
    /// properties relevant to debugging.
    /// </summary>
    public interface IProjectAdapter
    {
        /// <summary>
        /// Loads the project with the provided name from the large test samples folder.
        /// </summary>
        /// <param name="sampleName">The name of the sample that should be loaded.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if another project has already been loaded.
        /// </exception>
        /// <exception cref="ProjectLoadingException">
        /// Thrown if the project cannot be loaded.
        /// </exception>
        void Load(string sampleName);

        /// <summary>
        /// Builds the loaded project.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this method is called when no project has yet been loaded.
        /// </exception>
        /// <exception cref="ProjectBuildException">
        /// Thrown if the build procedure fails.
        /// </exception>
        void Build();

        // TODO: Use ConfiguredProject in VSFake.ProjectAdapter.
        /// <summary>
        /// Gets an <see cref="IAsyncProject"/> linked to the loaded project.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this getter is called when no project has yet been loaded.
        /// </exception>
        IAsyncProject Project { get; }

        /// <summary>
        /// Raised when a new project has been loaded.
        /// </summary>
        event ProjectLoadedHandler ProjectLoaded;
    }

    /// <summary>
    /// Represents an error trying to load a project.
    /// </summary>
    public class ProjectLoadingException : Exception
    {
        public ProjectLoadingException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    /// <summary>
    /// Represents an error trying to build a project.
    /// </summary>
    public class ProjectBuildException : Exception
    {
        public ProjectBuildException(string message) : base(message) { }
    }

    /// <summary>
    /// Callback function to be called when a project has been loaded.
    /// </summary>
    public delegate void ProjectLoadedHandler(ISolutionExplorerProject project);
}

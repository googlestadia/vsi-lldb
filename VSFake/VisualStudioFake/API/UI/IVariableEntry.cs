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

﻿using System.Collections.Generic;

namespace Google.VisualStudioFake.API.UI
{
    public enum VariableState
    {
        /// <summary>
        /// The variable has been evaluated and holds a value,
        /// but it may not reflect its actual current state.
        /// </summary>
        Evaluated,
        /// <summary>
        /// The variable has not been evaluated and does not hold a value.
        /// </summary>
        NotEvaluated,
        /// <summary>
        /// A refresh operation has been triggered, but not yet completed.
        /// </summary>
        Pending,
        /// <summary>
        /// The variable has been deleted.
        /// </summary>
        Deleted,
    }

    public interface IVariableEntry
    {
        #region Interop accessors

        string Name { get; }
        string Fullname { get; }
        string Value { get; }
        string StringView { get; }
        string Type { get; }

        bool IsReadOnly { get; }
        bool HasStringView { get; }
        bool IsExpandable { get; }
        bool HasSideEffects { get; }
        bool HasError { get; }
        bool RefreshRequired { get; }

        IList<IVariableEntry> GetChildren(int offset = 0, int count = int.MaxValue);

        #endregion

        /// <summary>
        /// Updates the internal state of the variable based on the current selected stack frame
        /// asynchronously.
        ///
        /// Example usage:
        ///
        /// var.Refresh();
        /// vsFake.RunUntil(() => var.Ready);
        /// Assert.That(var.Value, Is.EqualTo("new value"));
        ///
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if another refresh operation is pending or no stack frame is selected.
        /// </exception>
        /// <remarks>
        /// This method runs asynchronously.
        /// </remarks>
        IVariableEntry Refresh();

        /// <summary>
        /// Gets the current state of this variable.
        /// </summary>
        VariableState State { get; }

        /// <summary>
        /// Returns true if the variable is ready to be inspected.
        /// </summary>
        bool Ready { get; }
    }
}

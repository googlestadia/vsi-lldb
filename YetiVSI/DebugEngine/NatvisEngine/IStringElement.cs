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

namespace YetiVSI.DebugEngine.NatvisEngine
{
    /// <summary>
    /// Represents a Natvis element that holds a string.
    /// </summary>
    interface IStringElement
    {
        string Condition { get; }
        string IncludeView { get; }
        string ExcludeView { get; }
        bool Optional { get; }
        string Value { get; }
    }

    /// <summary>
    /// StringElement implementation for <DisplayString>.
    /// </summary>
    class DisplayStringElement : IStringElement
    {
        readonly DisplayStringType _displayString;

        public DisplayStringElement(DisplayStringType displayString)
        {
            if (displayString == null)
            {
                throw new ArgumentNullException(nameof(displayString));
            }

            _displayString = displayString;
        }

        public string Condition => _displayString.Condition;

        public string IncludeView => _displayString.IncludeView;

        public string ExcludeView => _displayString.ExcludeView;

        public bool Optional => _displayString.Optional;

        public string Value => _displayString.Value;
    }

    /// <summary>
    /// StringElement implementation for <StringView>.
    /// </summary>
    class StringViewElement : IStringElement
    {
        readonly StringViewType _stringView;

        public StringViewElement(StringViewType stringView)
        {
            if (stringView == null)
            {
                throw new ArgumentNullException(nameof(stringView));
            }

            _stringView = stringView;
        }

        public string Condition => _stringView.Condition;

        public string IncludeView => _stringView.IncludeView;

        public string ExcludeView => _stringView.ExcludeView;

        public bool Optional => _stringView.Optional;

        // In order to reuse the logic from display string, embed the value inside curly braces
        // given that the whole content of it should be treated as an expression.
        public string Value => $"{{{_stringView.Value}}}";
    }

    /// <summary>
    /// StringElement implementation for <SmartPointer>.
    /// </summary>
    class SmartPointerStringElement : IStringElement
    {
        readonly SmartPointerType _smartPointer;

        public SmartPointerStringElement(SmartPointerType smartPointer)
        {
            if (smartPointer == null)
            {
                throw new ArgumentNullException(nameof(smartPointer));
            }

            if (string.IsNullOrWhiteSpace(smartPointer.Value))
            {
                throw new ArgumentException(nameof(smartPointer.Value));
            }

            _smartPointer = smartPointer;
        }

        public string Condition => null;

        public string IncludeView => _smartPointer.IncludeView;

        public string ExcludeView => _smartPointer.ExcludeView;

        public bool Optional => _smartPointer.Optional;

        // In order to reuse the logic from display string, embed the value inside curly braces
        // given that the whole content of it should be treated as an expression.
        public string Value => $"{{{_smartPointer.Value}}}";
    }
}
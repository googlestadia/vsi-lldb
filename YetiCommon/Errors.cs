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

namespace YetiCommon
{
    /// <summary>
    /// Exceptions implementing this tag are reported as input errors.
    /// Indicates that the user provided invalid input as part of this action.
    /// </summary>
    public interface IInputError { }

    /// <summary>
    /// Concrete implementation of an input error
    /// </summary>
    public class InputException : Exception, IInputError
    {
        public InputException(string message) : base(message) { }
    }

    /// <summary>
    /// Exceptions implementing this tag are reported as configuration errors.
    /// Indicates that the user had an invalid configuration set up prior to this action.
    /// </summary>
    /// <remarks>
    /// This implies that the configuration was either missing, or it was read successufully,
    /// but a required value was missing, or that the value is not valid in this context.
    /// </remarks>
    public interface IConfigurationError { }

    /// <summary>
    /// Concrete implementation of a configuration error.
    /// </summary>
    public class ConfigurationException : Exception, IConfigurationError
    {
        public ConfigurationException(string message) : base(message) { }
        public ConfigurationException(string message, Exception inner)
            : base(message, inner) { }
    }

    /// <summary>
    /// Exceptions implementing this tag are reported as an invalid state error.
    /// Indicates that the object being acted on is in an invalid state as determined locally.
    /// </summary>
    /// <remarks>
    /// This implies an unexpected state, as opposed to ConfigurationException, which implies that
    /// the user should have known how to set up the state.
    /// </remarks>
    public interface IInvalidStateError { }

    /// <summary>
    /// Concrete implementation of an invalid state error.
    /// </summary>
    public class InvalidStateException : Exception, IInvalidStateError
    {
        public InvalidStateException(string message) : base(message) { }
    }

    /// <summary>
    /// An exception implements this interface to wrap user visible messages. When such exceptions
    /// are thrown, they should be caught, then messages should be displayed to users.
    /// </summary>
    public interface IUserVisibleError
    {
        // UserDetails is nullable
        string UserDetails { get; }
    }
}

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

﻿namespace YetiCommon.Tests.CastleAspects.TestSupport
{
    /// <summary>
    /// Simple domain object that wraps an integer.
    /// </summary>
    public interface IDummyObject
    {
        int GetValue();
        void SetValue(int val);
    }

    /// <summary>
    /// Simple domain object that wraps an integer. Includes various factories
    /// for the purpose of unit testing logic around factory validity.
    /// </summary>
    public class DummyObject : IDummyObject
    {
        /// <summary>
        /// Typical object factory which creates objects behind interfaces. Includes
        /// methods to test various create method formats.
        /// </summary>
        public class Factory
        {
            /// <summary>
            /// Creates a default DummyObject.
            /// </summary>
            public virtual IDummyObject Create()
            {
                return new DummyObject();
            }

            /// <summary>
            /// Creates a DummyObject wrapping a specified integer.
            /// </summary>
            /// <param name="val"></param>
            public virtual IDummyObject CreateWithArgument(int val)
            {
                return new DummyObject(val);
            }

            /// <summary>
            /// Creates DummyObject with initial value 0, through a nested create call.
            /// </summary>
            public virtual IDummyObject CreateWithNestedCall()
            {
                return CreateWithArgument(0);
            }

            /// <summary>
            /// Returns null.
            /// </summary>
            /// <returns>null</returns>
            public virtual IDummyObject CreateReturnsNull()
            {
                return null;
            }
        }

        public class FactoryWithNonVirtualMethod : Factory
        {
            /// <summary>
            /// Non-virtual method.
            /// </summary>
            public void CreateNonVirtualMethod()
            {
            }
        }

        public class FactoryWithVoidCreateMethod : Factory
        {
            /// <summary>
            /// Virtual void type create method.
            /// </summary>
            public virtual void CreateNothing()
            {
            }
        }

        public class FactoryWithCreateReturnType<T> : Factory
        {
            /// <summary>
            /// Creates a default version of the type specified.
            /// </summary>
            public virtual T CreateWithGivenReturnType()
            {
                return default(T);
            }
        }

        public class FactoryWithConcreteCreate : Factory
        {
            /// <summary>
            /// Creates a DummyObject, and returns the concrete implementation
            /// not behind a IDomainObject interface.
            /// </summary>
            public virtual DummyObject CreateConcrete()
            {
                return new DummyObject();
            }
        }

        public class FactoryWithPublicNonVirtualProperty : Factory
        {
            public int Property { get; set; }
        }

        public class FactoryWithPublicNonVirtualEvent : Factory
        {
            public delegate void VoidEventHandler();
#pragma warning disable 67 // disable event never used warning
            public event VoidEventHandler VoidEvent;
#pragma warning restore 67
        }

        public class FactoryWithVirtualMembers : Factory
        {
            public virtual int Property { get; set; }
            public delegate void VoidEventHandler();
#pragma warning disable 67 // disable event never used warning
            public virtual event VoidEventHandler VoidEvent;
#pragma warning restore 67
        }

        public class FactoryWithPrivateMembers : Factory
        {
            private int defaultValue = 0;
            private void InternalFunction()
            {
                defaultValue += 1;
            }
            public override IDummyObject Create()
            {
                InternalFunction();
                return new DummyObject(defaultValue);
            }
        }

        private int val;

        public DummyObject()
        {
        }

        public DummyObject(int val)
        {
            this.val = val;
        }

        /// <summary>
        /// Gets the underlying integer value.
        /// </summary>
        public virtual int GetValue()
        {
            return val;
        }

        /// <summary>
        /// Sets the underlying integer value.
        /// </summary>
        /// <param name="val"></param>
        public virtual void SetValue(int val)
        {
            this.val = val;
        }
    }
}

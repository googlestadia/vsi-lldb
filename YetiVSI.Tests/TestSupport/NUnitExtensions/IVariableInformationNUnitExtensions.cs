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

﻿using NUnit.Framework.Constraints;
using System.Collections.Generic;
using System.Linq;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.Test.TestSupport.NUnitExtensions
{
    /// <summary>
    /// Checks whether an IVariableInformation instance has exactly |childValues.Length| children
    /// with values given by |childValues| and in that order. Throws if an instance is passed that
    /// is null or not an IVariableInformation.
    /// </summary>
    public class HasChildrenWithValuesConstraint : Constraint
    {
        public string[] ChildValues { get; }

        public HasChildrenWithValuesConstraint(params string[] childValues) : base(childValues)
        {
            ChildValues = childValues;
        }

        public override ConstraintResult ApplyTo<TActual>(TActual actual)
        {
            var varInfo = actual as IVariableInformation;
            // Ignore "Synchronously waiting on tasks or awaiters may cause deadlocks. Use await or
            // JoinableTaskFactory.Run instead." since there's no async alternative. Revisit if this
            // becomes a problem.
#pragma warning disable VSTHRD002
            return new EqualConstraint(ChildValues).AsCollection.ApplyTo(
                varInfo.GetAllChildrenAsync().Result.Select(child => child.ValueAsync().Result)
                    .ToArray());
#pragma warning restore VSTHRD002
        }
    }

    /// <summary>
    /// Checks whether an IVariableInformation instance has at least one child. Throws if an
    /// instance is passed that is null or not an IVariableInformation.
    /// </summary>
    public class HasChildrenConstraint : Constraint
    {
        public override ConstraintResult ApplyTo<TActual>(TActual actual)
        {
            var varInfo = actual as IVariableInformation;
            // Ignore "Synchronously waiting on tasks or awaiters may cause deadlocks. Use await or
            // JoinableTaskFactory.Run instead." since there's no async alternative. Revisit if this
            // becomes a problem.
#pragma warning disable VSTHRD002
            return new NotConstraint(new EqualConstraint(0)).ApplyTo(
                varInfo.GetAllChildrenAsync().Result.Count);
#pragma warning restore VSTHRD002
        }
    }

    /// <summary>
    /// Allows you to write Assert.That(varInfo, Does.HaveChildWithValue("someValue"));
    /// </summary>
    public class Does : NUnit.Framework.Does
    {
        public static HasChildrenConstraint HaveChildren() => new HasChildrenConstraint();

        public static HasChildrenWithValuesConstraint HaveChildWithValue(string childValue) =>
            new HasChildrenWithValuesConstraint(childValue);

        public static HasChildrenWithValuesConstraint HaveChildrenWithValues(
            params string[] childValues) =>
            new HasChildrenWithValuesConstraint(childValues);
    }

    /// <summary>
    /// Allows you to write Assert.That(varInfo, Does.Not.HaveChildren()); etc.
    /// </summary>
    public static class CustomConstraintExtensions
    {
        public static HasChildrenConstraint HaveChildren(this ConstraintExpression expression)
        {
            var constraint = new HasChildrenConstraint();
            expression.Append(constraint);
            return constraint;
        }

        public static HasChildrenWithValuesConstraint HaveChildWithValue(
            this ConstraintExpression expression, string childValue)
        {
            var constraint = new HasChildrenWithValuesConstraint(childValue);
            expression.Append(constraint);
            return constraint;
        }

        public static HasChildrenWithValuesConstraint HaveChildrenWithValues(
            this ConstraintExpression expression, params string[] childValues)
        {
            var constraint = new HasChildrenWithValuesConstraint(childValues);
            expression.Append(constraint);
            return constraint;
        }
    }
}

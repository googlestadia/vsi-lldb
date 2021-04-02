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

﻿using NUnit.Framework;
using System;

namespace YetiVSI.Test
{
    [TestFixture]
    public class OptionPageGridTests
    {
        IExtensionOptions _optionPage;

        [SetUp]
        public void SetUp()
        {
            _optionPage = OptionPageGrid.CreateForTesting();
        }

        [Test]
        public void CaptureGameOutput()
        {
            // Unique because it's a bool option (not an enum).
            Assert.AreEqual(true, _optionPage.CaptureGameOutput);
        }

        [Test]
        public void SelectedAccount()
        {
            // Unique because it's a string option.
            Assert.AreEqual("", _optionPage.SelectedAccount);
        }

        [TestCase(nameof(OptionPageGrid.LLDBVisualizerSupport1),
                  nameof(IExtensionOptions.LLDBVisualizerSupport))]
        [TestCase(nameof(OptionPageGrid.SymbolServerSupport),
                  nameof(IExtensionOptions.SymbolServerSupport))]
        [TestCase(nameof(OptionPageGrid.NatvisLoggingLevel),
                  nameof(IExtensionOptions.NatvisLoggingLevel))]
        [TestCase(nameof(OptionPageGrid.FastExpressionEvaluation),
                  nameof(IExtensionOptions.FastExpressionEvaluation))]
        [TestCase(nameof(OptionPageGrid.ExpressionEvaluationEngine),
                  nameof(IExtensionOptions.ExpressionEvaluationEngine))]
        public void EnumOption(string optionName, string derivedName)
        {
            // Tests the validity of all enum options.
            var optionProperty = typeof(OptionPageGrid).GetProperty(optionName);
            var derivedProperty = typeof(IExtensionOptions).GetProperty(derivedName);

            foreach (var value in Enum.GetValues(optionProperty.PropertyType))
            {
                optionProperty.SetValue(_optionPage, value);
                Assert.IsNotNull(derivedProperty.GetValue(_optionPage));
            }
        }

        [Test]
        public void Options()
        {
            // Tests general sanity of Options data.
            foreach (var option in _optionPage.Options)
            {
                Assert.Multiple(() =>
                {
                    Assert.IsNotEmpty(option.Category);
                    Assert.IsNotEmpty(option.Name);
                    Assert.IsInstanceOf(option.Type, option.Value);
                    Assert.IsTrue(option.IsDefaultValue);
                });
            }
        }

        [Test]
        public void ResetSettings()
        {
            Assert.IsEmpty(_optionPage.SelectedAccount);

            OptionPageGrid page = (OptionPageGrid)_optionPage;
            page.SelectedAccount = "Dummy";
            Assert.AreEqual("Dummy", _optionPage.SelectedAccount);

            page.ResetSettings();
            Assert.IsEmpty(_optionPage.SelectedAccount);
        }
    }
}
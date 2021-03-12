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

﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace YetiVSI.Util
{
    public class SdkWarningFlagConverter : FeatureFlagConverter
    {
        readonly ShowOption[] _values =
            { ShowOption.AlwaysShow, ShowOption.NeverShow, ShowOption.AskForEachDialog };


        public SdkWarningFlagConverter() : base(typeof(ShowOption))
        {
        }

        public override StandardValuesCollection
            GetStandardValues(ITypeDescriptorContext context) =>
            new StandardValuesCollection(
                _values.Select(v => new SdkIncompatibilityWarning { ShowOption = v }).ToArray());

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture,
                                         object value, Type destinationType)
        {
            try
            {
                var sdkSetting = (SdkIncompatibilityWarning) value;
                if (context == null && destinationType == typeof(string) &&
                    culture.Equals(CultureInfo.InvariantCulture))
                {
                    // If it happens during settings saving.
                    return sdkSetting.ToString();
                }

                return base.ConvertTo(context, culture, sdkSetting.ShowOption, destinationType);
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Could not convert value '{value}' " +
                                $"of type '{nameof(SdkIncompatibilityWarning)}'" +
                                $" to type '{destinationType}'. " +
                                $"Exception: '{e.Message}'. Stack trace: '{e.StackTrace}'.");
            }

            return Convert.ToString(value);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture,
                                           object value)
        {
            try
            {
                if (context == null)
                {
                    try
                    {
                        return SdkIncompatibilityWarning.FromString((string) value);
                    }
                    catch
                    {
                        // Continue with the other method if parsing json failed.
                    }
                }

                var type = context.GetType().Assembly.GetTypes().First(
                    t => t.FullName == "System.Windows.Forms.PropertyGridInternal.GridEntry");
                var property =
                    type.GetProperty("PropertyValue", BindingFlags.Instance | BindingFlags.Public);
                var sdkWarningValue = (SdkIncompatibilityWarning) property.GetValue(context);

                sdkWarningValue.ShowOption = (ShowOption) base.ConvertFrom(context, culture, value);
                return sdkWarningValue;
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Could not convert value '{value}' " +
                                $"to type '{nameof(SdkIncompatibilityWarning)}'. " +
                                $"Exception: '{e.Message}'. Stack trace: '{e.StackTrace}'.");
            }

            return new SdkIncompatibilityWarning();
        }
    }
}

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

using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace YetiVSI.Util
{
    // Convertes enums to/from DescriptionAttribute strings.
    //
    // Usage example:
    //
    //   public enum WidgetFeatureFlag
    //   {
    //      [System.ComponentModel.Description("Default - Enabled")]
    //       DEFAULT,
    //      [System.ComponentModel.Description("Enabled")]
    //       ENABLED,
    //      [System.ComponentModel.Description("Disabled")]
    //       DISABLED
    //   }
    //
    //   [System.ComponentModel.TypeConverter(typeof(FeatureFlagConverter))]
    //   public WidgetFeatureFlag WidgetSupport { get; set; } = WidgetFeatureFlag.DEFAULT;
    //
    public class FeatureFlagConverter : EnumConverter
    {
        private Type _enumType;

        public FeatureFlagConverter(Type enumType) : base(enumType)
        {
            _enumType = enumType;
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(string);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture,
            object value, Type destinationType)
        {
            FieldInfo fieldInfo = _enumType.GetField(Enum.GetName(_enumType, value));
            DescriptionAttribute descriptionAttribute =
                (DescriptionAttribute)Attribute.GetCustomAttribute(fieldInfo,
                    typeof(DescriptionAttribute));
            if (descriptionAttribute != null)
            {
                return descriptionAttribute.Description;
            }
            return value.ToString();
        }

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture,
            object value)
        {
            foreach (FieldInfo fieldInfo in _enumType.GetFields())
            {
                DescriptionAttribute descriptionAttribute =
                    (DescriptionAttribute)Attribute.GetCustomAttribute(fieldInfo,
                        typeof(DescriptionAttribute));
                if (descriptionAttribute != null &&
                    ((string)value) == descriptionAttribute.Description)
                {
                    return Enum.Parse(_enumType, fieldInfo.Name);
                }
            }
            return Enum.Parse(_enumType, (string)value);
        }
    }
}

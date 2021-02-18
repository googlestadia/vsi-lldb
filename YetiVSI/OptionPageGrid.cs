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

﻿using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.Threading;
using YetiCommon;
using YetiVSI.DebugEngine;
using YetiVSI.DebugEngine.NatvisEngine;
using YetiVSI.Util;

namespace YetiVSI
{
    public enum LLDBVisualizerFeatureFlag
    {
        [Description("Default - Enabled")]
        [EnumValueAlias(ENABLED)]
        DEFAULT = 0,
        [Description("Enabled")]
        ENABLED = 1,
        [Description("Built-in only (Used for Stadia specific visualizations, e.g. SSE registers)")]
        BUILT_IN_ONLY = 2,
        [Description("Disabled")]
        DISABLED = 3,
    }

    public enum SymbolServerFeatureFlag
    {
        [Description("Default - Disabled")]
        [EnumValueAlias(DISABLED)]
        DEFAULT = 0,
        [Description("Enabled")]
        ENABLED = 1,
        [Description("Disabled")]
        DISABLED = 2,
    }

    public enum LunchGameApiFlowFlag
    {
        [Description("Default - Disabled")]
        [EnumValueAlias(DISABLED)]
        DEFAULT = 0,
        [Description("Enabled")]
        ENABLED = 1,
        [Description("Disabled")]
        DISABLED = 2,
    }

    public enum NatvisLoggingLevelFeatureFlag
    {
        [Description("Default - Disabled")]
        [EnumValueAlias(OFF)]
        DEFAULT = 0,
        [Description("Disabled")]
        OFF = 1,
        [Description("Error")]
        ERROR = 2,
        [Description("Warning")]
        WARNING = 3,
        [Description("Verbose")]
        VERBOSE = 4,
    }

    public enum FastExpressionEvaluationFlag
    {
        [Description("Default - Disabled")]
        [EnumValueAlias(DISABLED)]
        DEFAULT = 0,
        [Description("Enabled")]
        ENABLED = 1,
        [Description("Disabled")]
        DISABLED = 2,
    }

    public enum AsyncInterfacesFlag
    {
        [Description("Default - Enabled")]
        [EnumValueAlias(ENABLED)]
        DEFAULT = 0,
        [Description("Enabled")]
        ENABLED = 1,
        [Description("Disabled")]
        DISABLED = 2,
    }

    public enum ExpressionEvaluationEngineFlag {
        [Description("Default - LLDB")]
        [EnumValueAlias(LLDB)]
        DEFAULT = 0,
        [Description("LLDB")]
        LLDB = 1,
        [Description("lldb-eval with fallback to LLDB")]
        LLDB_EVAL_WITH_FALLBACK = 2,
        [Description("lldb-eval")]
        LLDB_EVAL = 3,
    }

    public struct GenericOption
    {
        public string Category;
        public string Name;
        public Type Type;
        public object Value;
        public bool IsDefaultValue;
    }

    public interface IExtensionOptions
    {
        event PropertyChangedEventHandler PropertyChanged;

        bool CaptureGameOutput { get; }
        string SelectedAccount { get; }
        LLDBVisualizerSupport LLDBVisualizerSupport { get; }
        SymbolServerSupport SymbolServerSupport { get; }
        LaunchGameApiFlow LaunchGameApiFlow { get; }
        NatvisLoggingLevel NatvisLoggingLevel { get; }
        FastExpressionEvaluation FastExpressionEvaluation { get; }
        ExpressionEvaluationEngine ExpressionEvaluationEngine { get; }
        AsyncInterfaces AsyncInterfaces { get; }

        IEnumerable<GenericOption> Options { get; }
    }

    /// <summary>
    /// Extension-wide options accessible via Tools->Options->Stadia SDK.
    /// </summary>
    /// <remarks>
    /// The name of this class is used as a key in the Windows Registry. Do not change it.
    ///
    /// The names, types, and values of these properties are stored in the registry.
    /// The names and values of some properties are also recorded in metrics.
    /// In general, do not rename or delete properties. Only add new ones.
    ///
    /// To change the default value of an enum option, modify the EnumValueAttribute on its
    /// DEFAULT value. For enum options, DefaultValue should point at the DEFAULT value.
    ///
    /// If you add a new option, be sure to add it to the unit tests.
    /// </remarks>
    public class OptionPageGrid : DialogPage, IExtensionOptions, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        NatvisLoggingLevelFeatureFlag _natvisLoggingLevel;
        FastExpressionEvaluationFlag _fastExpressionEvaluation;
        ExpressionEvaluationEngineFlag _expressionEvaluationEngine;
        AsyncInterfacesFlag _asyncInterfaces;

        [Category("Runtime")]
        [DisplayName("Display game output in Output window")]
        [Description("If enabled, captures the game's standard error and standard output and " +
            "displays it in the Visual Studio Output window.")]
        [DefaultValue(true)]
        public bool CaptureGameOutput { get; set; }

        [Category("Runtime")]
        [DisplayName("Account used to run game")]
        [Description("The account used to launch the game. If empty, the default account is " +
            "used.")]
        [DefaultValue("")]
        public string SelectedAccount { get; set; }

        [Category("LLDB Debugger")]
        [DisplayName("Enable variable visualizer support")]
        [Description("Enables Natvis visualizer support for inspecting variables.")]
        [TypeConverter(typeof(FeatureFlagConverter))]
        [DefaultValue(LLDBVisualizerFeatureFlag.DEFAULT)]
        public LLDBVisualizerFeatureFlag LLDBVisualizerSupport1 { get; set; }

        [Category("LLDB Debugger")]
        [DisplayName("Enable symbol server support")]
        [Description("If enabled, searches for symbol files in the locations specified in " +
            "'Tools -> Options... -> Debugging -> Symbols'.")]
        [TypeConverter(typeof(FeatureFlagConverter))]
        [DefaultValue(SymbolServerFeatureFlag.DEFAULT)]
        public SymbolServerFeatureFlag SymbolServerSupport { get; set; }

        [Category("LLDB Debugger")]
        [DisplayName("Enable launch anywhere")]
        [Description("If enabled, the game is launched via the new Launch Game API.")]
        [TypeConverter(typeof(FeatureFlagConverter))]
        [DefaultValue(LunchGameApiFlowFlag.DEFAULT)]
        public LunchGameApiFlowFlag LunchGameApiFlow { get; set; }

        [Category("LLDB Debugger")]
        [DisplayName("Natvis diagnostic messages")]
        [Description("Sets the level of diagnostic output involving visualization of C++" +
            " objects using natvis files. Takes effect immediately. Performance may be impacted" +
            " when enabled.")]
        [TypeConverter(typeof(FeatureFlagConverter))]
        [DefaultValue(NatvisLoggingLevelFeatureFlag.DEFAULT)]
        public NatvisLoggingLevelFeatureFlag NatvisLoggingLevel
        {
            get => _natvisLoggingLevel;
            set
            {
                if (value != _natvisLoggingLevel)
                {
                    _natvisLoggingLevel = value;
                    OnPropertyChanged(nameof(NatvisLoggingLevel));
                }
            }
        }

        [Category("LLDB Debugger")]
        [DisplayName("Fast expression evaluation")]
        [Description("Significantly faster expression evaluation, at the cost of incorrect " +
            "symbol resolution when there are name collisions between ivars and local " +
            "variables.")]
        [TypeConverter(typeof(FeatureFlagConverter))]
        [DefaultValue(FastExpressionEvaluationFlag.DEFAULT)]
        public FastExpressionEvaluationFlag FastExpressionEvaluation
        {
            get => _fastExpressionEvaluation;
            set
            {
                if (value != _fastExpressionEvaluation)
                {
                    _fastExpressionEvaluation = value;
                    OnPropertyChanged(nameof(FastExpressionEvaluation));
                }
            }
        }

        [Category("LLDB Debugger")]
        [DisplayName("Expression evaluation engine")]
        [Description("Sets the expression evaluation engine used by Visual Studio in the " +
            "Immediate Window and to display variables in various contexts (watches, autos, " +
            "NatVis). LLDB is the default, mostly feature complete, but slow; lldb-eval is a " +
            "faster experimental engine, more info: http://github.com/google/lldb-eval.")]
        [TypeConverter(typeof(FeatureFlagConverter))]
        [DefaultValue(ExpressionEvaluationEngineFlag.DEFAULT)]
        public ExpressionEvaluationEngineFlag ExpressionEvaluationEngine {
            get => _expressionEvaluationEngine;
            set
            {
                if (value != _expressionEvaluationEngine)
                {
                    _expressionEvaluationEngine = value;
                    OnPropertyChanged(nameof(ExpressionEvaluationEngine));
                }
            }
        }

        [Category("LLDB Debugger")]
        [DisplayName("Asynchronous expressions evaluation")]
        [Description("Evaluate some expressions in the background while stepping. This may lead " +
                     "to a more responsive debugging experience.")]
        [TypeConverter(typeof(FeatureFlagConverter))]
        [DefaultValue(AsyncInterfacesFlag.DEFAULT)]
        public AsyncInterfacesFlag AsyncInterfaces
        {
            get => _asyncInterfaces;
            set
            {
                if (value != _asyncInterfaces)
                {
                    _asyncInterfaces = value;
                    OnPropertyChanged(nameof(AsyncInterfaces));
                }
            }
        }

        public OptionPageGrid()
        {
            // Initialize options to default values, if specified.
            var optionsWithCategory = GetType().GetProperties()
                .Where(p => GetPropertyCategory(p) != null);
            foreach (var option in optionsWithCategory)
            {
                option.SetValue(this, GetPropertyDefaultValue(option));
            }
        }

        /// <summary>
        /// Creates an OptionPageGrid for use in tests. The constructor of OptionPageGrid calls
        /// ThreadHelper.JoinableTaskContext, but that's not set in tests. This method works
        /// around that.
        /// </summary>
        public static OptionPageGrid CreateForTesting()
        {
            // Disable "Use the ThreadHelper.JoinableTaskContext singleton rather than instantiating
            // your own to avoid deadlocks".
#pragma warning disable VSSDK005
            typeof(ThreadHelper)
                .GetField("_joinableTaskContextCache", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, new JoinableTaskContext());
#pragma warning restore VSSDK005

            return new OptionPageGrid();
        }

        #region IOptionPageGrid

        LLDBVisualizerSupport IExtensionOptions.LLDBVisualizerSupport =>
            EnumValueAliasAttribute.GetAliasOrValue(LLDBVisualizerSupport1)
                .ConvertTo<LLDBVisualizerSupport>();

        SymbolServerSupport IExtensionOptions.SymbolServerSupport =>
            EnumValueAliasAttribute.GetAliasOrValue(SymbolServerSupport)
                .ConvertTo<SymbolServerSupport>();

        LaunchGameApiFlow IExtensionOptions.LaunchGameApiFlow =>
            EnumValueAliasAttribute.GetAliasOrValue(LunchGameApiFlow)
                .ConvertTo<LaunchGameApiFlow>();

        NatvisLoggingLevel IExtensionOptions.NatvisLoggingLevel =>
            EnumValueAliasAttribute.GetAliasOrValue(NatvisLoggingLevel)
                .ConvertTo<NatvisLoggingLevel>();

        FastExpressionEvaluation IExtensionOptions.FastExpressionEvaluation =>
            EnumValueAliasAttribute.GetAliasOrValue(FastExpressionEvaluation)
                .ConvertTo<FastExpressionEvaluation>();

        ExpressionEvaluationEngine IExtensionOptions.ExpressionEvaluationEngine =>
            EnumValueAliasAttribute.GetAliasOrValue(ExpressionEvaluationEngine)
                .ConvertTo<ExpressionEvaluationEngine>();

        AsyncInterfaces IExtensionOptions.AsyncInterfaces =>
            EnumValueAliasAttribute.GetAliasOrValue(AsyncInterfaces)
                .ConvertTo<AsyncInterfaces>();

        IEnumerable<GenericOption> IExtensionOptions.Options
        {
            get
            {
                // Find all properties that are options with a category.
                var optionsWithCategory = GetType().GetProperties()
                    .Where(p => GetPropertyCategory(p) != null);

                // Fill in GenericOption for each option on this option page.
                return optionsWithCategory.Select(p =>
                {
                    var value = p.GetValue(this);
                    var realValue = value;
                    if (p.PropertyType.IsEnum)
                    {
                        // Map enum value aliases.
                        realValue = EnumValueAliasAttribute.GetAliasOrValue(p.PropertyType, value);
                    }
                    return new GenericOption
                    {
                        Category = GetPropertyCategory(p),
                        Name = p.Name,
                        Type = p.PropertyType,
                        Value = realValue,
                        IsDefaultValue = Equals(GetPropertyDefaultValue(p), value),
                    };
                });
            }
        }

        #endregion

        void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        static string GetPropertyCategory(PropertyInfo p)
        {
            var categoryAttribute =
                Attribute.GetCustomAttribute(p, typeof(CategoryAttribute)) as CategoryAttribute;
            return categoryAttribute?.Category;
        }

        static object GetPropertyDefaultValue(PropertyInfo p)
        {
            var defaultValueAttribute =
                Attribute.GetCustomAttribute(p, typeof(DefaultValueAttribute))
                    as DefaultValueAttribute;
            return defaultValueAttribute?.Value;
        }
    }
}

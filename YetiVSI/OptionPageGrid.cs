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

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using YetiCommon;
using YetiVSI.DebugEngine;
using YetiVSI.DebugEngine.NatvisEngine;
using YetiVSI.Util;

namespace YetiVSI
{
    public enum LLDBVisualizerFeatureFlag
    {
        [Description("Default - Enabled")] [EnumValueAlias(ENABLED)]
        DEFAULT = 0,
        [Description("Enabled")] ENABLED = 1,

        [Description("Built-in only (Used for Stadia specific visualizations, e.g. SSE registers)")]
        BUILT_IN_ONLY = 2,
        [Description("Disabled")] DISABLED = 3,
    }

    public enum SymbolServerFeatureFlag
    {
        [Description("Default - Disabled")] [EnumValueAlias(DISABLED)]
        DEFAULT = 0,
        [Description("Enabled")] ENABLED = 1,
        [Description("Disabled")] DISABLED = 2,
    }

    public enum LaunchGameApiFlowFlag
    {
        [Description("Default - Enabled")] [EnumValueAlias(ENABLED)]
        DEFAULT = 0,
        [Description("Enabled")] ENABLED = 1,
        [Description("Disabled - Deprecated")] DISABLED = 2,
    }

    public enum ShowOption
    {
        [Description("Default - Ask for each dialog")]
        AskForEachDialog,

        [Description("Never show")] NeverShow,

        [Description("Always show")] AlwaysShow,
    }

    public class SdkPairIdentifier
    {
        public string GameletVersion { get; set; }

        public string LocalVersion { get; set; }

        public string GameletName { get; set; }

        public override bool Equals(object obj)
        {
            if (!(obj is SdkPairIdentifier))
            {
                return false;
            }

            var sdkPair = (SdkPairIdentifier) obj;
            return Equals(sdkPair);
        }

        public override int GetHashCode() =>
            HashCode.Combine(GameletVersion, LocalVersion, GameletName);

        protected bool Equals(SdkPairIdentifier other)
        {
            if (other == null)
            {
                return false;
            }

            return string.Equals(GameletVersion, other.GameletVersion) &&
                string.Equals(LocalVersion, other.LocalVersion) &&
                string.Equals(GameletName, other.GameletName);

        }
    }

    public class SdkIncompatibilityWarning
    {
        ShowOption _showOption = ShowOption.AskForEachDialog;

        public List<SdkPairIdentifier> SdkVersionPairsToHide { get; set; } =
            new List<SdkPairIdentifier>();

        public ShowOption ShowOption
        {
            get => _showOption;
            set
            {
                if (_showOption == value)
                {
                    return;
                }

                _showOption = value;
                SdkVersionPairsToHide.Clear();
            }
        }

        public override string ToString() => new JsonUtil().Serialize(this);

        public static SdkIncompatibilityWarning FromString(string serialized) =>
            new JsonUtil().Deserialize<SdkIncompatibilityWarning>(serialized);

        public override bool Equals(object obj)
        {
            if (!(obj is SdkIncompatibilityWarning))
            {
                return false;
            }

            var sdkCompWarn = (SdkIncompatibilityWarning) obj;
            return Equals(sdkCompWarn);
        }

        protected bool Equals(SdkIncompatibilityWarning other)
        {
            if (other == null)
            {
                return false;
            }

            if (!other.ShowOption.Equals(ShowOption) ||
                other.SdkVersionPairsToHide.Count != SdkVersionPairsToHide.Count)
            {
                return false;
            }

            return other.SdkVersionPairsToHide.All(v => SdkVersionPairsToHide.Contains(v));
        }

        public override int GetHashCode() =>
            HashCode.Combine((int) _showOption, SdkVersionPairsToHide);
    }

    public enum NatvisLoggingLevelFeatureFlag
    {
        [Description("Default - Disabled")] [EnumValueAlias(OFF)]
        DEFAULT = 0,
        [Description("Disabled")] OFF = 1,
        [Description("Error")] ERROR = 2,
        [Description("Warning")] WARNING = 3,
        [Description("Verbose")] VERBOSE = 4,
    }

    public enum FastExpressionEvaluationFlag
    {
        [Description("Default - Disabled")] [EnumValueAlias(DISABLED)]
        DEFAULT = 0,
        [Description("Enabled")] ENABLED = 1,
        [Description("Disabled")] DISABLED = 2,
    }

    public enum ExpressionEvaluationEngineFlag
    {
        [Description("Default - lldb-eval with fallback to LLDB")]
        [EnumValueAlias(LLDB_EVAL_WITH_FALLBACK)]
        DEFAULT = 0,
        [Description("LLDB")] LLDB = 1,

        [Description("lldb-eval with fallback to LLDB")]
        LLDB_EVAL_WITH_FALLBACK = 2,
        [Description("lldb-eval")] LLDB_EVAL = 3,
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
        ExpressionEvaluationStrategy ExpressionEvaluationStrategy { get; }
        ShowOption SdkCompatibilityWarningOption { get; }
        void AddSdkVersionsToHide(string gameletVersion, string localVersion, string gameletName);
        bool SdkVersionsAreHidden(string gameletVersion, string localVersion, string gameletName);

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

        [Category("Game launch")]
        [DisplayName("Enable new launch flow")]
        [Description("If enabled, the game is launched via the new Launch Game API. " +
            "This option will be removed in 1.69 and the new Launch Game API " +
            "will be enabled for all launches.")]
        [TypeConverter(typeof(FeatureFlagConverter))]
        [DefaultValue(LaunchGameApiFlowFlag.DEFAULT)]
        public LaunchGameApiFlowFlag LaunchGameApiFlow { get; set; }

        [Category("Game launch")]
        [DisplayName("SDK incompatibility warning")]
        [Description("Sets whether the SDK incompatibility warning" +
            " is shown during a game launch or not.")]
        [TypeConverter(typeof(SdkWarningFlagConverter))]
        public SdkIncompatibilityWarning SdkIncompatibilityWarning { get; set; }

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
        public ExpressionEvaluationEngineFlag ExpressionEvaluationEngine
        {
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

        public OptionPageGrid()
        {
            ResetSettings();
        }

        /// <summary>
        /// Creates an OptionPageGrid for use in tests. The constructor of OptionPageGrid calls
        /// ThreadHelper.JoinableTaskContext, but that's not set in tests. This method works
        /// around that.
        /// </summary>
        public static OptionPageGrid CreateForTesting()
        {
            var taskContextField = typeof(ThreadHelper).GetField(
                "_joinableTaskContextCache", BindingFlags.Static | BindingFlags.NonPublic);

            // TODO: Use https://aka.ms/vssdktestfx for tests.
            if (taskContextField.GetValue(null) == null)
            {
                // Disable "Use the ThreadHelper.JoinableTaskContext singleton rather than
                // instantiating your own to avoid deadlocks".
#pragma warning disable VSSDK005
                taskContextField.SetValue(null, new JoinableTaskContext());
#pragma warning restore VSSDK005
            }

            return new OptionPageGrid();
        }

        public override void ResetSettings()
        {
            // Initialize options to default values, if specified.
            var optionsWithCategory = GetType().GetProperties()
                .Where(p => GetPropertyCategory(p) != null);
            foreach (var option in optionsWithCategory)
            {
                option.SetValue(this, GetPropertyDefaultValue(option));
            }
        }

        #region IOptionPageGrid

        LLDBVisualizerSupport IExtensionOptions.LLDBVisualizerSupport =>
            EnumValueAliasAttribute.GetAliasOrValue(LLDBVisualizerSupport1)
                .ConvertTo<LLDBVisualizerSupport>();

        SymbolServerSupport IExtensionOptions.SymbolServerSupport =>
            EnumValueAliasAttribute.GetAliasOrValue(SymbolServerSupport)
                .ConvertTo<SymbolServerSupport>();

        LaunchGameApiFlow IExtensionOptions.LaunchGameApiFlow =>
            EnumValueAliasAttribute.GetAliasOrValue(LaunchGameApiFlow)
                .ConvertTo<LaunchGameApiFlow>();

        NatvisLoggingLevel IExtensionOptions.NatvisLoggingLevel =>
            EnumValueAliasAttribute.GetAliasOrValue(NatvisLoggingLevel)
                .ConvertTo<NatvisLoggingLevel>();

        FastExpressionEvaluation IExtensionOptions.FastExpressionEvaluation =>
            EnumValueAliasAttribute.GetAliasOrValue(FastExpressionEvaluation)
                .ConvertTo<FastExpressionEvaluation>();

        ExpressionEvaluationStrategy IExtensionOptions.ExpressionEvaluationStrategy =>
            EnumValueAliasAttribute.GetAliasOrValue(ExpressionEvaluationEngine)
                .ConvertTo<ExpressionEvaluationStrategy>();

        ShowOption IExtensionOptions.SdkCompatibilityWarningOption =>
            SdkIncompatibilityWarning.ShowOption;

        void IExtensionOptions.AddSdkVersionsToHide(string gameletVersion, string localVersion,
                                                    string gameletName)
        {
            var sdkPair = new SdkPairIdentifier
            {
                GameletVersion = gameletVersion,
                LocalVersion = localVersion,
                GameletName = gameletName
            };
            if (SdkIncompatibilityWarning.SdkVersionPairsToHide.Contains(sdkPair))
            {
                return;
            }

            SdkIncompatibilityWarning.ShowOption = ShowOption.AskForEachDialog;
            SdkIncompatibilityWarning.SdkVersionPairsToHide.Add(sdkPair);
            SaveSettingsToStorage();
        }

        bool IExtensionOptions.SdkVersionsAreHidden(string gameletVersion, string localVersion,
                                                    string gameletName)
        {
            var sdkPair = new SdkPairIdentifier
            {
                GameletVersion = gameletVersion,
                LocalVersion = localVersion,
                GameletName = gameletName
            };
            return SdkIncompatibilityWarning.SdkVersionPairsToHide.Contains(sdkPair);
        }

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
            if (p.Name == nameof(SdkIncompatibilityWarning))
            {
                return new SdkIncompatibilityWarning();
            }

            var defaultValueAttribute =
                Attribute.GetCustomAttribute(p, typeof(DefaultValueAttribute)) as
                    DefaultValueAttribute;
            return defaultValueAttribute?.Value;
        }

        protected override void LoadSettingFromStorage(PropertyDescriptor prop)
        {
            // Sometimes it can happen that extension settings are saved in the wrong format or
            // some default values changed. The new extension will fail to load because it can't
            // convert the old settings. This shouldn't happen under normal conditions, but ignore
            // the load errors and fallback to using default values.
            try
            {
                base.LoadSettingFromStorage(prop);
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Failed to load a setting for {prop.Name}: {e}");
            }
        }
    }
}

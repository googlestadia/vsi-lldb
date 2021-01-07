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

using DebuggerApi;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace YetiVSI.DebugEngine.Variables
{
    /// <summary>
    /// Special purpose Natvis visualizers that are currently supported.
    /// </summary>
    public enum CustomVisualizer
    {
        None,
        SSE,
        SSE2,
    }

    public interface IVariableInformationFactory
    {
        /// <summary>
        /// Create an IVariableInformation object that will use the RemoteValue's real name.
        /// </summary>
        /// <param name="remoteValue">
        /// Value to wrap.</param>
        /// <param name="displayName">
        /// Name of the value as displayed in the UI. Uses the value's name if the parameter is not
        /// set. Useful when wrapping values that are the result of an expression valuation and may
        /// have a temporary name like "$1".
        /// </param>
        /// <param name="remoteValueFormat">
        /// Remote value formatter. Uses the default value format if the parameter is not set.
        /// </param>
        /// <param name="customVisualizer">
        /// Special purpose visualizer.
        /// </param>
        /// <returns>
        /// Wrapping IVariableInformation object
        /// </returns>
        IVariableInformation Create(RemoteValue remoteValue, string displayName = null,
                                    FormatSpecifier formatSpecifier = null,
                                    CustomVisualizer customVisualizer = CustomVisualizer.None);
    }

    /// <summary>
    /// Defines a concise representation of a variable.
    /// </summary>
    public interface IVariableInformation
    {
        /// <summary>
        /// Get the non-contextual name that can be evaluated within the scope of a stack frame.
        /// </summary>
        string Fullname();

        // The Visual Studio display name of the variable.
        string DisplayName { get; }

        // String that the debugger can send to the built-in text visualizer.
        string StringView { get; }

        // String that can be used for assignment operations involving this variable.
        string AssignmentValue { get; }

        /// <summary>
        /// Get serialized representation of the underlying value asynchronously.
        /// </summary>
        Task<string> ValueAsync();

        // Serialized representation of the variables type.
        string TypeName { get; }

        /// <summary>
        /// Gets the format specifier.
        /// </summary>
        string FormatSpecifier { get; }

        /// <summary>
        /// Value format used if no format specifier is defined.
        /// </summary>
        ValueFormat FallbackValueFormat { get; set; }

        /// <summary>
        /// Quick check to see if this might have children. MightHaveChildren() usually matches
        /// GetChildAdapter().CountChildren(1) != 0, but it is much quicker as it does not evaluate
        /// the first child. However, there might be false positives, e.g. if all children fail to
        /// evaluate or if all conditions evaluate to false, and possibly more. This is OK as this
        /// should be rare and the speed benefit can be significant. There should not be any false
        /// negatives.
        /// </summary>
        /// <returns>Returns true if this might have children.</returns>
        bool MightHaveChildren();

        /// <summary>
        /// Gets an adapter for querying the children of this variable.
        /// </summary>
        IChildAdapter GetChildAdapter();

        /// <summary>
        /// Special purpose Natvis visualizer. Used e.g. for some register sets. Mostly unused.
        /// </summary>
        CustomVisualizer CustomVisualizer { get; }

        /// <summary>
        /// Returns an enumerable of all ancestor data type names.  The first element is the actual
        /// typename of this and the enumerable is ordered such that the closer ancestors are
        /// earlier in the enumerable than the more distant ancestors.  Ordering of ancestors with
        /// the same distance is undefined.
        ///
        /// If the variable is a pointer or reference, the method returns all inherited types
        /// of the pointee.
        /// </summary>
        IEnumerable<string> GetAllInheritedTypes();

        bool Error { get; }

        string ErrorMessage { get; }

        uint ErrorCode { get;  }

        bool IsPointer { get; }

        bool IsReference { get; }

        bool IsTruthy { get; }

        // Determine if the varInfo is a null pointer. Returns true if the varInfo
        // is a pointer and its value is a hex representation of zero, false otherwise.
        bool IsNullPointer();

        /// <summary>
        /// Expands nested expressions like .a->b[0].c[1]->d.
        /// </summary>
        /// <param name="vsExpression">The expression path relative to this.</param>
        /// <returns>A new </returns>
        IVariableInformation GetValueForExpressionPath(VsExpression vsExpression);

        /// <summary>
        /// Evaluates an expression asynchronously in the same context as this variable.
        /// </summary>
        /// <param name="name">Display name of the resulting variable.</param>
        /// <param name="vsExpression">The expression to evaluate.</param>
        /// <returns></returns>
        Task<IVariableInformation> CreateValueFromExpressionAsync(string name,
                                                                  VsExpression vsExpression);

        /// <summary>
        /// Evaluates an expression asynchronously in the context of a variable.
        /// <param name="name">Display name of the resulting variable.</param>
        /// <param name="expression">The expression to evaluate.</param>
        /// <returns></returns>
        /// </summary>
        Task<IVariableInformation> EvaluateExpressionAsync(string displayName,
            VsExpression expression);

        /// <summary>
        /// Evaluates an expression asynchronously in the context of a variable using lldb-eval.
        /// </summary>
        /// <param name="displayName">Display name of the resulting variable.</param>
        /// <param name="expression">The expression to evaluate.</param>
        /// <returns></returns>
        Task<IVariableInformation> EvaluateExpressionLldbEvalAsync(string displayName,
            VsExpression expression);

        /// <summary>
        /// Dereferences a variable if it is a pointer.
        /// </summary>
        IVariableInformation Dereference();

        /// <summary>
        /// Retrieves the child variable by |name|.
        /// </summary>
        /// <param name="name">The name of the child variable relative to this.</param>
        /// <returns>The child or null if it doesn't exist.</returns>
        IVariableInformation FindChildByName(string name);

        bool IsReadOnly { get; }

        /// <summary>
        /// Update the value of this variable.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="error">An error description if unsuccessful.</param>
        /// <returns>True if successful.</returns>
        bool Assign(string value, out string error);

        /// <summary>
        /// Returns address of the memory context. This is used to display the relevant memory
        /// in Visual Studio's memory view.
        /// </summary>
        ulong ? GetMemoryContextAddress();

        /// <summary>
        /// Gets the memory address as a hex string, e.g., "0x00c0ffee".
        /// The current format affects the formatting as follows: the uppercase number
        /// formats (e.g., 'X') cause the letters to be uppercase ("0x00C0FFEE"),
        /// the no-prefix formats (e.g., 'xb') remove the "0x" prefix ("00C0FFEE").
        /// Returns the empty string if the value is not a pointer or reference.
        /// </summary>
        string GetMemoryAddressAsHex();

        // TODO: Find a way to remove caching concerns from IVariableInformation.
        /// <summary>
        /// Get a new IVariableInformation that bulk prefetches its fields.
        /// </summary>
        /// <returns>The prefetched variable.</returns>
        IVariableInformation GetCachedView();
    }

    // Decorator class that delegates all calls to the contained IVariableInformation instance.
    // Makes decorator subclassing easier.
    public abstract class VariableInformationDecorator : IVariableInformation
    {
        protected VariableInformationDecorator(IVariableInformation varInfo)
        {
            VarInfo = varInfo;
        }

        #region IVariableInformation

        public bool Error => VarInfo.Error;

        public string ErrorMessage => VarInfo.ErrorMessage;

        public uint ErrorCode => VarInfo.ErrorCode;

        public bool IsReadOnly => VarInfo.IsReadOnly;

        public bool IsPointer => VarInfo.IsPointer;

        public bool IsReference => VarInfo.IsReference;

        public bool IsTruthy => VarInfo.IsTruthy;

        public virtual string DisplayName => VarInfo.DisplayName;

        public string TypeName => VarInfo.TypeName;

        public string AssignmentValue => VarInfo.AssignmentValue;

        public virtual async Task<string> ValueAsync() => await VarInfo.ValueAsync();

        public virtual string StringView => VarInfo.StringView;

        public string FormatSpecifier => VarInfo.FormatSpecifier;

        public virtual ValueFormat FallbackValueFormat
        {
            get => VarInfo.FallbackValueFormat;
            set => VarInfo.FallbackValueFormat = value;
        }

        public CustomVisualizer CustomVisualizer => VarInfo.CustomVisualizer;

        public bool IsNullPointer() => VarInfo.IsNullPointer();

        public virtual bool MightHaveChildren() => VarInfo.MightHaveChildren();

        public virtual IChildAdapter GetChildAdapter() => VarInfo.GetChildAdapter();

        public virtual IEnumerable<string> GetAllInheritedTypes() =>
            VarInfo.GetAllInheritedTypes();

        public IVariableInformation GetValueForExpressionPath(VsExpression vsExpression) =>
            VarInfo.GetValueForExpressionPath(vsExpression);

        public async Task<IVariableInformation> CreateValueFromExpressionAsync(
            string name, VsExpression vsExpression) =>
            await VarInfo.CreateValueFromExpressionAsync(name, vsExpression);

        public async Task<IVariableInformation> EvaluateExpressionAsync(string displayName,
                                                                        VsExpression expression) =>
            await VarInfo.EvaluateExpressionAsync(displayName, expression);

        public async Task<IVariableInformation> EvaluateExpressionLldbEvalAsync(
            string displayName, VsExpression expression) =>
            await VarInfo.EvaluateExpressionLldbEvalAsync(displayName, expression);

        public IVariableInformation Dereference() => VarInfo.Dereference();

        public IVariableInformation FindChildByName(string name) => VarInfo.FindChildByName(name);

        public bool Assign(string value, out string error) => VarInfo.Assign(value, out error);

        public ulong? GetMemoryContextAddress() => VarInfo.GetMemoryContextAddress();

        public virtual string Fullname() => VarInfo.Fullname();

        public virtual string GetMemoryAddressAsHex() => VarInfo.GetMemoryAddressAsHex();

        public abstract IVariableInformation GetCachedView();

        #endregion

        protected IVariableInformation VarInfo { get; }
    }

    // Overrides the variable name of a wrapped IVariableInformation object.
    public class NamedVariableInformation : VariableInformationDecorator
    {
        string displayName;

        public NamedVariableInformation(IVariableInformation varInfo, string displayName) :
            base(varInfo)
        {
            this.displayName = displayName;
        }

        public override string DisplayName => displayName;

        public override IVariableInformation GetCachedView() =>
            new NamedVariableInformation(VarInfo.GetCachedView(), displayName);
    }

    // IVariableInformation stub used for showing passive errors in the Visual Studio UI.
    public class ErrorVariableInformation : IVariableInformation
    {
        public string value;

        public ErrorVariableInformation(string displayName, string value)
        {
            DisplayName = displayName;
            this.value = value;
        }

        #region IVariableInformation

        public bool Error => true;

        public string ErrorMessage => "";

        public uint ErrorCode => 0;

        public bool IsReadOnly => true;

        public bool IsPointer => false;

        public bool IsReference => false;

        public bool IsTruthy => false;

        public string DisplayName { get; }

        public string Fullname() => null;

        public string TypeName => "";

        public string AssignmentValue => value;

        public string Value => value;

        public Task<string> ValueAsync() => Task.FromResult(Value);

        public string FormatSpecifier => "";

        public ValueFormat FallbackValueFormat
        {
            get => ValueFormat.Default;
            set
            {
                /* empty */
            }
        }

        public CustomVisualizer CustomVisualizer => CustomVisualizer.None;

        public string StringView => "";

        // TODO: In order to support aspect decorators we can't expose 'this'.
        public IVariableInformation GetCachedView() => this;

        public bool IsNullPointer() => false;

        public bool Assign(string value, out string error) => throw new NotImplementedException();

        public IVariableInformation GetValueForExpressionPath(VsExpression vsExpression) =>
            throw new NotImplementedException();

        public Task<IVariableInformation> CreateValueFromExpressionAsync(
            string name, VsExpression vsExpression) => throw new NotImplementedException();

        public Task<IVariableInformation> EvaluateExpressionAsync(string displayName,
                                                                  VsExpression expression)
        {
            throw new NotImplementedException();
        }

        public Task<IVariableInformation> EvaluateExpressionLldbEvalAsync(string displayName,
                                                                          VsExpression expression)
        {
            throw new NotImplementedException();
        }

        public IVariableInformation Dereference()
        {
            throw new NotImplementedException();
        }

        public IVariableInformation FindChildByName(string name) =>
            throw new NotImplementedException();

        public IEnumerable<string> GetAllInheritedTypes() => throw new NotImplementedException();

        public bool MightHaveChildren() => false;

        public IChildAdapter GetChildAdapter() =>
            new ListChildAdapter.Factory().Create(new List<IVariableInformation>());

        public ulong? GetMemoryContextAddress() => throw new NotImplementedException();
        public string GetMemoryAddressAsHex() => throw new NotImplementedException();

        #endregion
    }

    // Creates simple IVariableInformation objects that wrap Debugger.RemoteValue objects.
    public class LLDBVariableInformationFactory : IVariableInformationFactory
    {
        readonly IRemoteValueChildAdapterFactory _childAdapterFactory;
        VarInfoBuilder _varInfoBuilder;

        public LLDBVariableInformationFactory(IRemoteValueChildAdapterFactory childAdapterFactory)
        {
            _childAdapterFactory = childAdapterFactory;
        }

        public void SetVarInfoBuilder(VarInfoBuilder varInfoBuilder)
        {
            _varInfoBuilder = varInfoBuilder;
        }

        #region IVariableInformationFactory

        public virtual IVariableInformation Create(
            RemoteValue remoteValue, string displayName = null,
            FormatSpecifier formatSpecifier = null,
            CustomVisualizer customVisualizer =
                CustomVisualizer
                    .None) => new RemoteValueVariableInformation(_varInfoBuilder,
                                                                 formatSpecifier != null
                                                                     ? formatSpecifier.Expression
                                                                     : string.Empty,
                                                                 RemoteValueFormatProvider.Get(
                                                                     formatSpecifier?.Expression,
                                                                     formatSpecifier?.Size),
                                                                 ValueFormat.Default, remoteValue,
                                                                 displayName
                                                                 ?? remoteValue.GetName(),
                                                                 customVisualizer,
                                                                 _childAdapterFactory);

#endregion
    }

    /// <summary>
    /// A simple IVariableInformation wrapper for DebuggerApi.RemoteValues.
    /// </summary>
    public class RemoteValueVariableInformation : IVariableInformation
    {
        readonly VarInfoBuilder _varInfoBuilder;

        readonly RemoteValue _remoteValue;
        readonly IRemoteValueFormat _remoteValueFormat;
        readonly IRemoteValueChildAdapterFactory _childAdapterFactory;

        public RemoteValueVariableInformation(VarInfoBuilder varInfoBuilder, string formatSpecifier,
                                              IRemoteValueFormat remoteValueFormat,
                                              ValueFormat fallbackValueFormat,
                                              RemoteValue remoteValue, string displayName,
                                              CustomVisualizer customVisualizer,
                                              IRemoteValueChildAdapterFactory childAdapterFactory)
        {
            _varInfoBuilder = varInfoBuilder;
            _remoteValueFormat = remoteValueFormat;
            _remoteValue = remoteValue;
            _childAdapterFactory = childAdapterFactory;

            DisplayName = displayName;
            FormatSpecifier = formatSpecifier;
            FallbackValueFormat = fallbackValueFormat;
            CustomVisualizer = customVisualizer;
        }

        public string Fullname() => _remoteValue.GetValueType() == DebuggerApi.ValueType.Register
            ? $"{ExpressionConstants.RegisterPrefix}{_remoteValue.GetName()}"
            : _remoteValue.GetFullName();

        /// <summary>
        /// Context specific display name of the underlying variable.
        /// </summary>
        public string DisplayName { get; }

        public string AssignmentValue =>
            _remoteValueFormat.GetValueForAssignment(_remoteValue, FallbackValueFormat);

        public string Value => GetErrorString() ??
            _remoteValueFormat.FormatValue(_remoteValue, FallbackValueFormat);

        public Task<string> ValueAsync() => Task.FromResult(Value);

        public string StringView => !Error
            ? _remoteValueFormat.FormatStringView(_remoteValue, FallbackValueFormat) ?? ""
            : "";

        public string TypeName => Error ? "" : _remoteValue.GetTypeName();

        public string FormatSpecifier { get; }

        public ValueFormat FallbackValueFormat { get; set; }

        public CustomVisualizer CustomVisualizer { get; }

        public bool MightHaveChildren() => _remoteValueFormat.GetNumChildren(_remoteValue) != 0;

        public IChildAdapter GetChildAdapter() => _childAdapterFactory.Create(_remoteValue,
                                                                              _remoteValueFormat,
                                                                              _varInfoBuilder,
                                                                              FormatSpecifier);

        public IEnumerable<string> GetAllInheritedTypes()
        {
            SbType typeInfo = _remoteValue.GetTypeInfo();
            if (typeInfo == null)
            {
                yield break;
            }

            TypeFlags typeFlags = typeInfo.GetTypeFlags();
            if (typeFlags.HasFlag(TypeFlags.IS_POINTER) ||
                typeFlags.HasFlag(TypeFlags.IS_REFERENCE))
            {
                typeInfo = typeInfo.GetPointeeType();
                if (typeInfo == null)
                {
                    yield break;
                }
            }

            var typeQueue = new Queue<SbType>();
            typeQueue.Enqueue(typeInfo);

            while (typeQueue.Count > 0)
            {
                SbType curType = typeQueue.Dequeue();
                yield return curType.GetName();
                uint numDirectBaseClasses = curType.GetNumberOfDirectBaseClasses();
                for (uint i = 0; i < numDirectBaseClasses; i++)
                {
                    typeQueue.Enqueue(curType.GetDirectBaseClassAtIndex(i).GetTypeInfo());
                }
            }
        }

        public bool Error => _remoteValue.GetError().Fail();

        public string ErrorMessage => _remoteValue.GetError().GetCString() ?? "";

        public uint ErrorCode => _remoteValue.GetError().GetError();

        public bool IsPointer => _remoteValue.TypeIsPointerType();

        public bool IsReference
        {
            get {
                TypeFlags typeFlags = _remoteValue.GetTypeInfo()?.GetTypeFlags() ?? TypeFlags.NONE;
                return typeFlags.HasFlag(TypeFlags.IS_REFERENCE);
            }
        }

        public bool IsTruthy
        {
            get
            {
                if (IsPointer)
                {
                    return !IsNullPointer();
                }

                // Check for bool "true". Do the type check only if the value is actually "true"
                // since GetTypeInfo() and GetCanonicalType() are potentially expensive (RPC call).
                // Be sure to use ValueFormat.Default here, double.TryParse below wouldn't work for
                // other formats like hex.
                string value = _remoteValue.GetValue(ValueFormat.Default);
                if (value == "true")
                {
                    SbType canonicalType = _remoteValue.GetTypeInfo().GetCanonicalType();
                    return canonicalType.GetName() == "bool";
                }

                double doubleResult;
                if (double.TryParse(value, out doubleResult))
                {
                    return doubleResult != 0;
                }

                return false;
            }
        }

        public bool IsNullPointer()
        {
            // Strip the hex prefix if it is present.
            // Be sure to use ValueFormat.Default here, just in case.
            string hexValue = _remoteValue.GetValue(ValueFormat.Default);
            if (hexValue.StartsWith("0x") || hexValue.StartsWith("0X"))
            {
                hexValue = hexValue.Substring(2);
            }

            int intVal;
            if (!int.TryParse(hexValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                              out intVal))
            {
                return false;
            }

            return intVal == 0 && IsPointer;
        }

        public IVariableInformation GetValueForExpressionPath(VsExpression vsExpression)
        {
            RemoteValue expressionValue =
                _remoteValue.GetValueForExpressionPath(vsExpression.Value);

            if (expressionValue == null)
            {
                return null;
            }

            return _varInfoBuilder.Create(expressionValue,
                                          formatSpecifier: vsExpression.FormatSpecifier);
        }

        public async Task<IVariableInformation> CreateValueFromExpressionAsync(string displayName,
                                                                               VsExpression
                                                                                   vsExpression)
        {
            RemoteValue expressionValue =
                await _remoteValue.CreateValueFromExpressionAsync(displayName, vsExpression.Value);

            if (expressionValue == null)
            {
                return null;
            }

            return _varInfoBuilder.Create(expressionValue,
                                          formatSpecifier: vsExpression.FormatSpecifier);
        }

        public async Task<IVariableInformation> EvaluateExpressionAsync(string displayName,
                                                                        VsExpression vsExpression)
        {
            RemoteValue resultValue =
                await _remoteValue.EvaluateExpressionAsync(vsExpression.Value);
            return resultValue == null
                       ? null
                       : _varInfoBuilder.Create(resultValue, displayName,
                                                formatSpecifier: vsExpression.FormatSpecifier);
        }

        public async Task<IVariableInformation> EvaluateExpressionLldbEvalAsync(string displayName,
                                                                                VsExpression vsExpression)
        {
            RemoteValue resultValue =
                await _remoteValue.EvaluateExpressionLldbEvalAsync(vsExpression.Value);
            return resultValue == null
                       ? null
                       : _varInfoBuilder.Create(resultValue, displayName,
                                                formatSpecifier: vsExpression.FormatSpecifier);
        }

        public IVariableInformation Dereference()
        {
            RemoteValue dereferencedValue = _remoteValue.Dereference();
            return dereferencedValue == null ? null : _varInfoBuilder.Create(dereferencedValue);
        }

        public IVariableInformation FindChildByName(string name)
        {
            RemoteValue child = _remoteValue.GetChildMemberWithName(name);
            if (child == null)
            {
                return null;
            }

            return _varInfoBuilder.Create(child, name);
        }

        public bool IsReadOnly => _remoteValue.GetVariableAssignExpression() == null;

        public bool Assign(string expression, out string error)
        {
            if (IsReadOnly)
            {
                error = string.Format(
                    "Attempted to assign a value to a read only variable with name '{0}'.",
                    DisplayName);

                return false;
            }

            string cast = "";
            if (_remoteValue.TypeIsPointerType() ||
                _remoteValue.GetTypeInfo().GetTypeFlags().HasFlag(TypeFlags.IS_ENUMERATION))
            {
                cast = $"({_remoteValue.GetTypeInfo().GetCanonicalType().GetName()})";
            }

            expression = _remoteValueFormat.FormatExpressionForAssignment(_remoteValue, expression);
            // Avoid using parentheses to enclose registers because they can prevent assignments
            // involving initialization lists from succeeding.
            if (_remoteValue.GetValueType() != DebuggerApi.ValueType.Register)
            {
                expression = $"({expression})";
            }

            RemoteValue tempValue = _remoteValue.CreateValueFromExpression(
                DisplayName, $"{_remoteValue.GetVariableAssignExpression()} = {cast}{expression}");

            SbError sbError = tempValue.GetError();
            error = sbError.Fail() ? sbError.GetCString() : null;
            return sbError.Success();
        }

        public ulong? GetMemoryContextAddress() => _remoteValue.GetMemoryContextAddress();

        public string GetMemoryAddressAsHex() =>
            _remoteValueFormat.FormatValueAsAddress(_remoteValue);

        public IVariableInformation GetCachedView() => new RemoteValueVariableInformation(
            _varInfoBuilder, FormatSpecifier, _remoteValueFormat, FallbackValueFormat,
            _remoteValue.GetCachedView(_remoteValueFormat.GetValueFormat(FallbackValueFormat)),
            DisplayName, CustomVisualizer, _childAdapterFactory);

        /// <summary>
        /// Returns an error string if the _remoteValue's error is in fail state and null otherwise.
        /// </summary>
        string GetErrorString()
        {
            SbError error = _remoteValue.GetError();
            if (!error.Fail())
            {
                return null;
            }

            // TODO: Determine why we are suppressing error strings for REGISTER
            // ValueTypes.  Add comments if needed or remove otherwise.
            string errorString = _remoteValue.GetValueType() == DebuggerApi.ValueType.Register
                ? "unavailable"
                : error.GetCString();

            return $"<{errorString}>";
        }
    }

    /// <summary>
    /// Represents "[More]" element used when a variable has more children than it is configured
    /// to return on expand.
    /// </summary>
    public class MoreVariableInformation : IVariableInformation
    {
        readonly IChildAdapter _childAdapter;

        public MoreVariableInformation(IChildAdapter childAdapter)
        {
            _childAdapter = childAdapter;
        }

        public string Fullname() => "[More]";

        public string DisplayName => "[More]";
        public string StringView => "";
        public string AssignmentValue => "";

        // Returns empty value so that there is no preview for [More].
        public string Value => " ";
        public Task<string> ValueAsync() => Task.FromResult(" ");

        public string TypeName => "";
        public string FormatSpecifier => "";

        public ValueFormat FallbackValueFormat
        {
            get => ValueFormat.Default;
            set
            {
                /* empty */
            }
        }

        public bool Assign(string value, out string error) => throw new NotImplementedException();

        public ulong? GetMemoryContextAddress() => null;

        public string GetMemoryAddressAsHex() => "";

        public IVariableInformation GetCachedView() => this;

        public bool MightHaveChildren() => true;

        public IChildAdapter GetChildAdapter() => _childAdapter;
        public CustomVisualizer CustomVisualizer => CustomVisualizer.None;
        public IEnumerable<string> GetAllInheritedTypes() => Enumerable.Empty<string>();

        public bool Error => false;
        public string ErrorMessage => null;
        public uint ErrorCode => 0;
        public bool IsPointer => false;
        public bool IsReference => false;
        public bool IsTruthy => false;
        public bool IsNullPointer() => false;

        public IVariableInformation GetValueForExpressionPath(VsExpression vsExpression) =>
            throw new NotImplementedException();

        public Task<IVariableInformation> CreateValueFromExpressionAsync(
            string name, VsExpression vsExpression) => throw new NotImplementedException();

        public Task<IVariableInformation> EvaluateExpressionAsync(
            string displayName, VsExpression expression) =>
            throw new NotImplementedException();

        public Task<IVariableInformation> EvaluateExpressionLldbEvalAsync(
            string displayName, VsExpression expression) =>
            throw new NotImplementedException();

        public IVariableInformation Dereference() => throw new NotImplementedException();

        public IVariableInformation FindChildByName(string name) =>
            throw new NotImplementedException();

        public bool IsReadOnly => true;
    }

    public class CachingVariableInformation : VariableInformationDecorator
    {
        IChildAdapter _cachedChildAdapter;
        IVariableInformation _cachedVarInfo;
        string _cachedStringView;

        public CachingVariableInformation(IVariableInformation varInfo) : base(varInfo)
        {
        }

        public override string StringView
        {
            get
            {
                if (_cachedStringView == null)
                {
                    _cachedStringView = VarInfo.StringView;
                }

                return _cachedStringView;
            }
        }

        public override ValueFormat FallbackValueFormat
        {
            get => VarInfo.FallbackValueFormat;
            set
            {
                if (VarInfo.FallbackValueFormat != value)
                {
                    InvalidateCaches();
                }

                VarInfo.FallbackValueFormat = value;
            }
        }

        public override IChildAdapter GetChildAdapter()
        {
            if (_cachedChildAdapter == null)
            {
                _cachedChildAdapter = VarInfo.GetChildAdapter();
            }

            return _cachedChildAdapter;
        }

        public override IVariableInformation GetCachedView()
        {
            if (_cachedVarInfo == null)
            {
                _cachedVarInfo = new CachingVariableInformation(VarInfo.GetCachedView());
            }

            return _cachedVarInfo;
        }

        void InvalidateCaches()
        {
            _cachedChildAdapter = null;
            _cachedVarInfo = null;
            _cachedStringView = null;
        }
    }
}

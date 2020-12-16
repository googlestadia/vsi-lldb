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
using System.Threading.Tasks;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.DebugEngine.NatvisEngine
{
    /// <summary>
    /// Creates IVariableInformation objects that rely on Natvis visualizer information to implement
    /// GetChildren() and Value if the variable has a custom visualizer. Relies on another factory
    /// to create the root IVariableInformation objects.
    /// </summary>
    public class CustomVisualizerVariableInformationFactory : IVariableInformationFactory
    {
        readonly NatvisExpander _natvisExpander;
        readonly IVariableInformationFactory _varInfoFactory;
        readonly ExpandVariableInformation.Factory _expandVariableFactory;

        public CustomVisualizerVariableInformationFactory(NatvisExpander natvisExpander,
                                                          IVariableInformationFactory
                                                              varInfoFactory,
                                                          ExpandVariableInformation.Factory
                                                              expandVariableFactory)
        {
            _natvisExpander = natvisExpander;
            _varInfoFactory = varInfoFactory;
            _expandVariableFactory = expandVariableFactory;
        }

        public virtual IVariableInformation Create(
            RemoteFrame remoteFrame, RemoteValue remoteValue, string displayName = null,
            FormatSpecifier formatSpecifier = null,
            CustomVisualizer customVisualizer = CustomVisualizer.None)
        {
            IVariableInformation varInfo = _varInfoFactory.Create(
                remoteFrame, remoteValue, displayName, formatSpecifier, customVisualizer);

            if (customVisualizer != CustomVisualizer.None)
            {
                varInfo = new NatvisVariableInformation(_natvisExpander, varInfo);
            }

            if (ExpandFormatSpecifierUtil.TryParseExpandFormatSpecifier(varInfo.FormatSpecifier,
                                                                        out int expandedIndex))
            {
                varInfo = _expandVariableFactory.Create(varInfo, expandedIndex);
            }

            return new CachingVariableInformation(varInfo);
        }
    }

    // Creates IVariableInformation objects that rely on Natvis visualizer information to implement
    // GetChildren() and Value.  Relies on another factory to create the root IVariableInformation
    // objects.
    public class NatvisVariableInformationFactory : IVariableInformationFactory
    {
        readonly NatvisExpander _natvisExpander;
        readonly IVariableInformationFactory _varInfoFactory;
        readonly ExpandVariableInformation.Factory _expandVariableFactory;

        public NatvisVariableInformationFactory(NatvisExpander natvisExpander,
                                                IVariableInformationFactory varInfoFactory,
                                                ExpandVariableInformation.Factory
                                                    expandVariableFactory)
        {
            _natvisExpander = natvisExpander;
            _varInfoFactory = varInfoFactory;
            _expandVariableFactory = expandVariableFactory;
        }

        // TODO: handle the situation when raw and expand formatter are both present
        public virtual IVariableInformation Create(
            RemoteFrame remoteFrame, RemoteValue remoteValue, string displayName = null,
            FormatSpecifier formatSpecifier = null,
            CustomVisualizer customVisualizer = CustomVisualizer.None)
        {
            IVariableInformation varInfo = _varInfoFactory.Create(
                remoteFrame, remoteValue, displayName, formatSpecifier, customVisualizer);

            // Don't use Natvis for raw format specifier (!), e.g. "myvar,!".
            if (FormatSpecifierUtil.HasRawFormatSpecifier(varInfo.FormatSpecifier))
            {
                return new CachingVariableInformation(varInfo);
            }

            var natvisVarInfo = new NatvisVariableInformation(_natvisExpander, varInfo);
            if (ExpandFormatSpecifierUtil.TryParseExpandFormatSpecifier(
                    natvisVarInfo.FormatSpecifier, out int expandedIndex))
            {
                return new CachingVariableInformation(
                    _expandVariableFactory.Create(natvisVarInfo, expandedIndex));
            }

            return new CachingVariableInformation(natvisVarInfo);
        }
    }

    // Overrides GetChildren() and Value behavior by using Natvis value formatting information.
    // Also, it overrides GetCachedView() so the resulting view holds a reference to Natvis.
    public class NatvisVariableInformation : VariableInformationDecorator
    {
        readonly NatvisExpander _natvisExpander;

        public NatvisVariableInformation(NatvisExpander natvisExpander,
                                         IVariableInformation varInfo) : base(varInfo)
        {
            _natvisExpander = natvisExpander;
        }

        public override async Task<string> ValueAsync() => UseNatvis
            ? await _natvisExpander.StringFormatter.FormatDisplayStringAsync(VarInfo)
            : await VarInfo.ValueAsync();

        public override bool MightHaveChildren() => UseNatvis
            ? _natvisExpander.MightHaveChildren(VarInfo)
            : VarInfo.MightHaveChildren();

        public override IChildAdapter GetChildAdapter() =>
            UseNatvis ? _natvisExpander.GetChildAdapter(VarInfo) : VarInfo.GetChildAdapter();

        public override string StringView => UseNatvis
            ? _natvisExpander.StringFormatter.FormatStringView(VarInfo)
            : VarInfo.StringView;

        public override IVariableInformation GetCachedView() =>
            new NatvisVariableInformation(_natvisExpander, VarInfo.GetCachedView());

        bool UseNatvis => _natvisExpander.IsTypeViewVisible(VarInfo);
    }

    public class RawChildVariableInformation : VariableInformationDecorator
    {
        public RawChildVariableInformation(IVariableInformation varInfo) : base(varInfo)
        {
        }

        public override string DisplayName => "[Raw View]";

        public override IVariableInformation GetCachedView() =>
            new RawChildVariableInformation(VarInfo.GetCachedView());
    }
}
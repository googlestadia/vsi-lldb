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
using System.Threading.Tasks;
using YetiCommon.CastleAspects;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.DebugEngine.NatvisEngine
{
    public class NatvisExpander : SimpleDecoratorSelf<NatvisExpander>
    {
        readonly NatvisCollectionEntity.Factory _natvisCollectionFactory;
        readonly SmartPointerEntity.Factory _smartPointerFactory;

        public NatvisStringFormatter StringFormatter { get; }
        public NatvisVisualizerScanner VisualizerScanner { get; }

        public NatvisExpander(NatvisCollectionEntity.Factory natvisCollectionFactory,
                              SmartPointerEntity.Factory smartPointerFactory,
                              NatvisStringFormatter stringFormatter,
                              NatvisVisualizerScanner visualizerScanner)
        {
            _natvisCollectionFactory = natvisCollectionFactory;
            _smartPointerFactory = smartPointerFactory;

            StringFormatter = stringFormatter;
            VisualizerScanner = visualizerScanner;
        }

        internal async Task<bool> MightHaveChildrenAsync(IVariableInformation variable)
        {
            VisualizerInfo visualizer = await VisualizerScanner.FindTypeAsync(variable);
            ExpandType expandType = visualizer?.GetExpandType();
            if (expandType != null)
            {
                if (expandType.Items != null && expandType.Items.Length > 0)
                {
                    return true;
                }

                return !expandType.HideRawView && await variable.MightHaveChildrenAsync();
            }

            SmartPointerType smartPointerType = visualizer?.GetSmartPointerType();
            return smartPointerType != null || await variable.MightHaveChildrenAsync();
        }

        /// <summary>
        /// Returns false if |variable| does not have a Natvis visualizer or the visualizer defines
        /// IncludeView/ExcludeView attributes and the view should be hidden based on
        /// |variable.FormatSpecifier|. Returns true in all other cases.
        /// </summary>
        internal async Task<bool> IsTypeViewVisibleAsync(IVariableInformation variable)
        {
            VisualizerType visualizer =
                (await VisualizerScanner.FindTypeAsync(variable))?.Visualizer;
            return visualizer != null &&
                NatvisViewsUtil.IsViewVisible(variable.FormatSpecifier, visualizer.IncludeView,
                                              visualizer.ExcludeView);
        }

        internal async Task<IChildAdapter> GetChildAdapterAsync(IVariableInformation variable)
        {
            VisualizerInfo visualizer = await VisualizerScanner.FindTypeAsync(variable);
            if (visualizer?.Visualizer.Items == null)
            {
                return await variable.GetChildAdapterAsync();
            }

            IChildAdapter childAdapter = await CreateNatvisChildAdapterAsync(visualizer, variable);

            // Special case for SSE registers. VS calls VariableInformationEnum.Count, even if
            // the SSE registers are not visible. Without this, we would have to expand all
            // children just to get the count, which slows down the register window a lot.
            if (variable.CustomVisualizer == CustomVisualizer.SSE ||
                variable.CustomVisualizer == CustomVisualizer.SSE2)
            {
                return new SseAdapter(visualizer.GetExpandType().Items.Length, childAdapter);
            }

            return childAdapter;
        }

        async Task<IChildAdapter> CreateNatvisChildAdapterAsync(VisualizerInfo visualizer,
                                                                IVariableInformation variable)
        {
            ExpandType expandType = visualizer.GetExpandType();
            if (expandType != null)
            {
                return _natvisCollectionFactory.Create(variable, expandType,
                                                       visualizer.NatvisScope);
            }

            SmartPointerType smartPointerType = visualizer.GetSmartPointerType();
            if (smartPointerType != null)
            {
                return _smartPointerFactory.Create(variable, smartPointerType,
                                                   visualizer.NatvisScope,
                                                   await variable.GetChildAdapterAsync());
            }

            return await variable.GetChildAdapterAsync();
        }
    }
}
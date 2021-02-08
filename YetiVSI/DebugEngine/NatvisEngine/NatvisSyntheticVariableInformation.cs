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

﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.DebugEngine.NatvisEngine
{
    // Used for <Synthetic> items.
    class NatvisSyntheticVariableInformation : VariableInformationDecorator
    {
        readonly NatvisStringFormatter _stringFormatter;
        readonly NatvisCollectionEntity.Factory _natvisCollectionFactory;
        readonly NatvisScope _natvisScope;
        readonly SyntheticItemType _syntheticItemType;
        readonly string _displayValue;

        internal NatvisSyntheticVariableInformation(
            NatvisStringFormatter stringFormatter,
            NatvisCollectionEntity.Factory natvisCollectionFactory, NatvisScope natvisScope,
            SyntheticItemType syntheticItemType, IVariableInformation varInfo, string displayValue)
            : base(varInfo)
        {
            _stringFormatter = stringFormatter;
            _natvisCollectionFactory = natvisCollectionFactory;
            _natvisScope = natvisScope;
            _syntheticItemType = syntheticItemType;
            _displayValue = displayValue;

            // Synthetic items should never show the raw view.
            if (syntheticItemType.Expand != null)
            {
                syntheticItemType.Expand.HideRawView = true;
            }
        }

        #region VariableInformationDecorator

        public override string DisplayName => _syntheticItemType.Name;

        public override async Task<string> ValueAsync() => await Task.FromResult(_displayValue);

        public override bool MightHaveChildren()
        {
            // In contrast to regular variables, synthetic variables don't ever show raw view.
            ExpandType expandType = _syntheticItemType.Expand;
            return expandType?.Items != null && expandType.Items.Length > 0;
        }

        public override IChildAdapter GetChildAdapter() =>
            _natvisCollectionFactory.Create(VarInfo, _syntheticItemType.Expand, _natvisScope);

        public override IVariableInformation GetCachedView() =>
            new NatvisSyntheticVariableInformation(_stringFormatter, _natvisCollectionFactory,
                                                   _natvisScope, _syntheticItemType,
                                                   VarInfo.GetCachedView(), _displayValue);

        public override string StringView
        {
            get
            {
                NatvisStringFormatter.FormatStringContext formatStringContext =
                    _syntheticItemType.StringView == null
                        ? new NatvisStringFormatter.FormatStringContext()
                        : new NatvisStringFormatter.FormatStringContext {
                              StringElements = _syntheticItemType.StringView.Select(
                                  e => new StringViewElement(e)),
                              NatvisScope = _natvisScope
                          };
                return _stringFormatter.FormatStringView(formatStringContext, VarInfo);
            }
        }

        #endregion
    }
}
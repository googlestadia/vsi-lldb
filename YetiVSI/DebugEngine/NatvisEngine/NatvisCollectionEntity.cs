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
using System.Collections.Generic;
using System.Threading.Tasks;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.DebugEngine.NatvisEngine
{
    public class NatvisCollectionEntity : INatvisEntity
    {
        public class Factory
        {
            const int _maxChildrenPerRangeIndexListItems = 50000;
            const int _maxChildrenPerRangeArrayItems = 50000;
            const int _maxChildrenPerRangeLinkedListItems = 100;
            const int _maxChildrenPerRangeTreeItems = 100;
            const int _maxChildrenPerRangeCustomListItems = 20;

            readonly ItemEntity.Factory _itemFactory;
            readonly SyntheticItemEntity.Factory _syntheticItemFactory;
            readonly ExpandedItemEntity.Factory _expandedItemFactory;
            readonly IndexListItemsEntity.Factory _indexListItemsFactory;
            readonly ArrayItemsEntity.Factory _arrayItemsFactory;
            readonly LinkedListItemsEntity.Factory _linkedListItemsFactory;
            readonly TreeItemsEntity.Factory _treeItemsFactory;
            readonly CustomListItemsEntity.Factory _customListItemsFactory;

            readonly NatvisDiagnosticLogger _logger;
            readonly Func<bool> _natvisExperimentsEnabled;

            public Factory(ItemEntity.Factory itemFactory,
                           SyntheticItemEntity.Factory syntheticItemFactory,
                           ExpandedItemEntity.Factory expandedItemFactory,
                           IndexListItemsEntity.Factory indexListItemsFactory,
                           ArrayItemsEntity.Factory arrayItemsFactory,
                           LinkedListItemsEntity.Factory linkedListItemsFactory,
                           TreeItemsEntity.Factory treeItemsFactory,
                           CustomListItemsEntity.Factory customListItemsFactory,
                           NatvisDiagnosticLogger logger, Func<bool> natvisExperimentsEnabled)
            {
                _itemFactory = itemFactory;
                _syntheticItemFactory = syntheticItemFactory;
                _expandedItemFactory = expandedItemFactory;
                _indexListItemsFactory = indexListItemsFactory;
                _arrayItemsFactory = arrayItemsFactory;
                _linkedListItemsFactory = linkedListItemsFactory;
                _treeItemsFactory = treeItemsFactory;
                _customListItemsFactory = customListItemsFactory;

                _logger = logger;
                _natvisExperimentsEnabled = natvisExperimentsEnabled;
            }

            public INatvisEntity Create(IVariableInformation variable, ExpandType expandType,
                                        IDictionary<string, string> scopedName)
            {
                var children = new List<INatvisEntity>();
                if (expandType?.Items == null)
                {
                    return new NatvisCollectionEntity(children, variable,
                                                      expandType?.HideRawView ?? true);
                }

                foreach (object item in expandType.Items)
                {
                    if (item is ItemType itemType)
                    {
                        children.Add(_itemFactory.Create(variable, scopedName, itemType));
                    }
                    else if (item is SyntheticItemType syntheticItemType)
                    {
                        children.Add(_syntheticItemFactory.Create(variable, scopedName,
                                                                  syntheticItemType, this));
                    }
                    else if (item is ExpandedItemType expandedItemType)
                    {
                        children.Add(_expandedItemFactory.Create(variable, scopedName,
                                                                 expandedItemType));
                    }
                    else if (item is IndexListItemsType indexListItems)
                    {
                        children.Add(RangedNatvisEntityDecorator.First(
                                         _maxChildrenPerRangeIndexListItems,
                                         _indexListItemsFactory.Create(
                                             variable, scopedName, indexListItems)));
                    }
                    else if (item is ArrayItemsType arrayItems)
                    {
                        children.Add(RangedNatvisEntityDecorator.First(
                                         _maxChildrenPerRangeArrayItems,
                                         _arrayItemsFactory.Create(
                                             variable, scopedName, arrayItems)));
                    }
                    else if (item is LinkedListItemsType linkedListItems)
                    {
                        children.Add(RangedNatvisEntityDecorator.First(
                                         _maxChildrenPerRangeLinkedListItems,
                                         _linkedListItemsFactory.Create(
                                             variable, scopedName, linkedListItems)));
                    }
                    else if (item is TreeItemsType treeItems)
                    {
                        children.Add(RangedNatvisEntityDecorator.First(
                                         _maxChildrenPerRangeTreeItems,
                                         _treeItemsFactory.Create(
                                             variable, scopedName, treeItems)));
                    }
                    else if (item is CustomListItemsType customListItems &&
                        _natvisExperimentsEnabled())
                    {
                        children.Add(RangedNatvisEntityDecorator.First(
                                         _maxChildrenPerRangeCustomListItems,
                                         _customListItemsFactory.Create(
                                             variable, scopedName, customListItems)));
                    }
                    else
                    {
                        children.Add(
                            new UnsupportedNatvisEntity(variable, item.GetType(), _logger));
                    }
                }

                return new NatvisCollectionEntity(children, variable, expandType.HideRawView);
            }

            public INatvisEntity CreateFromChildrenList(IList<INatvisEntity> children,
                                                        IVariableInformation variable,
                                                        bool hideRawView) =>
                new NatvisCollectionEntity(children, variable, hideRawView);
        }

        readonly IList<INatvisEntity> _expandableChildren;
        readonly IVariableInformation _variable;

        readonly bool _hideRawView;

        NatvisCollectionEntity(IList<INatvisEntity> expandableChildren,
                               IVariableInformation variable, bool hideRawView)
        {
            _expandableChildren = expandableChildren;
            _variable = variable;
            _hideRawView = hideRawView;
        }


        public async Task<int> CountChildrenAsync()
        {
            int count = 0;
            foreach (INatvisEntity child in _expandableChildren)
            {
                count += await child.CountChildrenAsync();
            }

            return await ShowRawViewAsync() ? count + 1 : count;
        }

        public async Task<IList<IVariableInformation>> GetChildrenAsync(int from, int count)
        {
            int childFrom = 0;

            var result = new List<IVariableInformation>();
            foreach (INatvisEntity child in _expandableChildren)
            {
                if (childFrom >= from + count)
                {
                    break;
                }

                int childCount = await child.CountChildrenAsync();
                if (Intersect(from, count, childFrom, childCount, out int interFrom,
                              out int interCount))
                {
                    result.AddRange(
                        await child.GetChildrenAsync(interFrom - childFrom, interCount));
                }

                childFrom += childCount;
            }

            if (result.Count < count && await ShowRawViewAsync())
            {
                result.Add(new RawChildVariableInformation(_variable));
            }

            return result;
        }

        public async Task<bool> IsValidAsync()
        {
            foreach (INatvisEntity child in _expandableChildren)
            {
                if (!await child.IsValidAsync())
                {
                    return false;
                }
            }

            return true;
        }

        async Task<bool> ShowRawViewAsync() => !_hideRawView || !await IsValidAsync();

        /// <summary>
        /// Intersects [from1,from1+count1) with [from2,from2+count2).
        /// Returns false if the intersection is empty.
        /// </summary>
        bool Intersect(int from1, int count1, int from2, int count2, out int interFrom,
                       out int interCount)
        {
            interFrom = Math.Max(from1, from2);
            interCount = Math.Min(from1 + count1, from2 + count2) - interFrom;
            return interCount > 0;
        }
    }
}
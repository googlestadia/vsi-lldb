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

﻿using DebuggerApi;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace YetiVSI.DebugEngine.Variables
{
    public interface IRemoteValueChildAdapterFactory
    {
        IChildAdapter Create(RemoteValue remoteValue, IRemoteValueFormat remoteValueFormat,
                             RemoteFrame remoteFrame, VarInfoBuilder varInfoBuilder,
                             string formatSpecifier);
    }

    class RemoteValueRangedAdapter : RangedChildAdapterDecorator
    {
        readonly RemoteValueChildAdapter _entity;

        RemoteValueRangedAdapter(int offset, int maxChildren, RemoteValueChildAdapter entity) :
            base(entity, maxChildren, offset)
        {
            _entity = entity;
        }

        public static IChildAdapter First(int maxChildren, RemoteValueChildAdapter entity) =>
            new RemoteValueRangedAdapter(0, maxChildren, entity);

        protected override IChildAdapter More(int newOffset, int maxChildren) =>
            new RemoteValueRangedAdapter(newOffset, maxChildren, _entity);
    }

    /// <summary>
    /// Implementation of IChildAdapter used for RemoteValues. Can get the number of children
    /// efficiently and access any range of children without having to enumerate the whole set.
    /// </summary>
    public class RemoteValueChildAdapter : IChildAdapter
    {
        public class Factory : IRemoteValueChildAdapterFactory
        {
            const int _remoteValueCountPerRange = 50000;

            public IChildAdapter Create(RemoteValue remoteValue,
                                        IRemoteValueFormat remoteValueFormat,
                                        RemoteFrame remoteFrame, VarInfoBuilder varInfoBuilder,
                                        string formatSpecifier) =>
                CreateForTesting(remoteValue, remoteValueFormat, remoteFrame, varInfoBuilder,
                                 formatSpecifier, _remoteValueCountPerRange);

            public IChildAdapter CreateForTesting(RemoteValue remoteValue,
                                                  IRemoteValueFormat remoteValueFormat,
                                                  RemoteFrame remoteFrame,
                                                  VarInfoBuilder varInfoBuilder,
                                                  string formatSpecifier, int maxCountPerRange) =>
                RemoteValueRangedAdapter.First(
                    maxCountPerRange,
                    new RemoteValueChildAdapter(remoteValue, remoteValueFormat, remoteFrame,
                                                varInfoBuilder, formatSpecifier));
        }

        readonly RemoteValue _remoteValue;
        readonly IRemoteValueFormat _remoteValueFormat;
        readonly RemoteFrame _remoteFrame;
        readonly VarInfoBuilder _varInfoBuilder;
        readonly string _formatSpecifier;

        RemoteValueChildAdapter(RemoteValue remoteValue, IRemoteValueFormat remoteValueFormat,
                                RemoteFrame remoteFrame, VarInfoBuilder varInfoBuilder,
                                string formatSpecifier)
        {
            _remoteValue = remoteValue;
            _remoteValueFormat = remoteValueFormat;
            _varInfoBuilder = varInfoBuilder;
            _remoteFrame = remoteFrame;
            _formatSpecifier = formatSpecifier;
        }

        public Task<int> CountChildrenAsync() =>
            Task.FromResult((int) _remoteValueFormat.GetNumChildren(_remoteValue));

        // TODO Convert RemoteValueChildAdapter to async.
        public Task<IList<IVariableInformation>> GetChildrenAsync(int from, int count)
        {
            string childFormatSpecifier =
                FormatSpecifierUtil.GetChildFormatSpecifier(_formatSpecifier, _remoteValueFormat);

            IList<IVariableInformation> result =
                _remoteValueFormat.GetChildren(_remoteValue, from, count)
                    .Select(v => _varInfoBuilder.Create(
                                _remoteFrame, v,
                                formatSpecifier: new FormatSpecifier(childFormatSpecifier)))
                    .ToList();

            return Task.FromResult(result);
        }
    }
}
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

﻿using YetiCommon.CastleAspects;

namespace YetiCommon.Tests.CastleAspects.TestSupport
{
    /// <summary>
    /// A domain object test double that does not implement ISelfType directly.
    /// </summary>
    public class TargetObj : IDecoratorSelf<ISelfType>
    {
        #region IDecoratorSelf<ISelfType>

        public virtual ISelfType Self { get; set; }

        #endregion
    }

    /// <summary>
    /// A domain object test double that does implement ISelfType directly.
    /// </summary>
    public class TargetObjImpl : IDecoratorSelf<ISelfType>, ISelfType
    {
        #region IDecoratorSelf<ISelfType>

        public virtual ISelfType Self { get; set; }

        #endregion

        #region ISelfType

        public virtual int Something { get; set; } = 0;

        public virtual int DoSomething()
        {
            return ++Something;
        }

        #endregion
    }
}

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

using NUnit.Framework;
using YetiVSI.Util;

namespace YetiVSI.Test.Util
{
    public enum DummyFeatureFlag
    {
        [System.ComponentModel.Description("DummyDescription")]
        WITH_DESCRIPTION,
        NO_DESCRIPTION
    }

    [TestFixture]
    class FeatureFlagConverterTests
    {
        [Test]
        public void StringConversions()
        {
            var converter = new FeatureFlagConverter(typeof(DummyFeatureFlag));

            Assert.That(converter.CanConvertTo(typeof(string)), Is.True);
            Assert.That(converter.CanConvertFrom(typeof(string)), Is.True);

            Assert.That(converter.ConvertFromString("DummyDescription"),
                Is.EqualTo(DummyFeatureFlag.WITH_DESCRIPTION));
            Assert.That(converter.ConvertFromString("NO_DESCRIPTION"),
                Is.EqualTo(DummyFeatureFlag.NO_DESCRIPTION));

            Assert.That(converter.ConvertToString(DummyFeatureFlag.WITH_DESCRIPTION),
                Is.EqualTo("DummyDescription"));
            Assert.That(converter.ConvertToString(DummyFeatureFlag.NO_DESCRIPTION),
                Is.EqualTo("NO_DESCRIPTION"));
        }
    }
}
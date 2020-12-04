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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using YetiCommon.Cloud;

namespace YetiCommon.Tests.Cloud
{
    [TestFixture]
    class ConfigStatusTests
    {
        static readonly ConfigStatus.ErrorLevel[] _sortedLevels = {
            ConfigStatus.ErrorLevel.Ok, ConfigStatus.ErrorLevel.Warning,
            ConfigStatus.ErrorLevel.Error
        };

        [Test]
        public void CreateOkStatusTest()
        {
            ConfigStatus status = ConfigStatus.OkStatus();
            Assert.That(status.IsOk, Is.EqualTo(true));
            Assert.That(status.IsWarningLevel, Is.EqualTo(false));
            Assert.That(status.IsErrorLevel, Is.EqualTo(false));
            Assert.That(status.AllMessages, Is.Empty);
            Assert.That(status.ErrorMessages, Is.Empty);
            Assert.That(status.WarningMessages, Is.Empty);
            Assert.That(status.WarningMessage, Is.Empty);
            Assert.That(status.SeverityLevel, Is.EqualTo(ConfigStatus.ErrorLevel.Ok));
            Assert.That(status.MessagesByErrorLevel(ConfigStatus.ErrorLevel.Ok), Is.Null);
            Assert.That(status.MessagesByErrorLevel(ConfigStatus.ErrorLevel.Warning), Is.Empty);
            Assert.That(status.MessagesByErrorLevel(ConfigStatus.ErrorLevel.Error), Is.Empty);
        }

        [Test]
        public void CreateWarningStatusTest()
        {
            string message = "warning Message";
            ConfigStatus status = ConfigStatus.WarningStatus(message);
            Assert.That(status.IsOk, Is.EqualTo(false));
            Assert.That(status.IsWarningLevel, Is.EqualTo(true));
            Assert.That(status.IsErrorLevel, Is.EqualTo(false));
            Assert.That(status.AllMessages, Is.EqualTo(new [] { message }));
            Assert.That(status.ErrorMessages, Is.Empty);
            Assert.That(status.WarningMessages, Is.EqualTo(new[] { message }));
            Assert.That(status.WarningMessage, Is.EqualTo(message));
            Assert.That(status.SeverityLevel, Is.EqualTo(ConfigStatus.ErrorLevel.Warning));
            Assert.That(status.MessagesByErrorLevel(ConfigStatus.ErrorLevel.Ok), Is.Null);
            Assert.That(status.MessagesByErrorLevel(ConfigStatus.ErrorLevel.Warning),
                        Is.EqualTo(new[] { message }));
            Assert.That(status.MessagesByErrorLevel(ConfigStatus.ErrorLevel.Error), Is.Empty);
        }

        [Test]
        public void CreateErrorStatusTest()
        {
            string message = "error Message";
            ConfigStatus status = ConfigStatus.ErrorStatus(message);
            Assert.That(status.IsOk, Is.EqualTo(false));
            Assert.That(status.IsWarningLevel, Is.EqualTo(false));
            Assert.That(status.IsErrorLevel, Is.EqualTo(true));
            Assert.That(status.AllMessages, Is.EqualTo(new[] { message }));
            Assert.That(status.ErrorMessages, Is.EqualTo(new[] { message }));
            Assert.That(status.WarningMessages, Is.Empty);
            Assert.That(status.WarningMessage, Is.Empty);
            Assert.That(status.SeverityLevel, Is.EqualTo(ConfigStatus.ErrorLevel.Error));
            Assert.That(status.MessagesByErrorLevel(ConfigStatus.ErrorLevel.Ok), Is.Null);
            Assert.That(status.MessagesByErrorLevel(ConfigStatus.ErrorLevel.Warning), Is.Empty);
            Assert.That(status.MessagesByErrorLevel(ConfigStatus.ErrorLevel.Error),
                        Is.EqualTo(new[] { message }));
        }

        [TestCase(ConfigStatus.ErrorLevel.Error, TestName = "ToErrorLevel")]
        [TestCase(ConfigStatus.ErrorLevel.Warning, TestName = "ToWarningLevel")]
        [TestCase(ConfigStatus.ErrorLevel.Ok, TestName = "ToOkLevel")]
        public void AppendWarningTest(ConfigStatus.ErrorLevel initialLevel)
        {
            int initialLevelNum = Array.IndexOf(_sortedLevels, initialLevel);
            int warningNum = Array.IndexOf(_sortedLevels, ConfigStatus.ErrorLevel.Warning);
            int newLevelNum = Math.Max(initialLevelNum, warningNum);
            ConfigStatus.ErrorLevel newLevel = _sortedLevels[newLevelNum];
            string initialMessage = "Message init";
            string warningMessage = "Warning message";

            ConfigStatus status = StatusBySeverityLevel(initialLevel, initialMessage);
            status.AppendWarning(warningMessage);
            Assert.That(status.SeverityLevel, Is.EqualTo(newLevel));
            Assert.That(status.IsOk, Is.EqualTo(false));
            Assert.That(status.IsWarningLevel,
                        Is.EqualTo(newLevel == ConfigStatus.ErrorLevel.Warning));
            Assert.That(status.IsErrorLevel, Is.EqualTo(newLevel == ConfigStatus.ErrorLevel.Error));
            string[] errorMessages = GetMessagesForLevel(ConfigStatus.ErrorLevel.Error,
                  new[] { initialLevel, ConfigStatus.ErrorLevel.Warning },
                  new[] { initialMessage, warningMessage });
            string[] warningMessages = GetMessagesForLevel(ConfigStatus.ErrorLevel.Warning,
                  new[] { initialLevel, ConfigStatus.ErrorLevel.Warning },
                  new[] { initialMessage, warningMessage });
            Assert.That(status.AllMessages,
                        Is.EquivalentTo(errorMessages.Concat(warningMessages)));
            Assert.That(status.ErrorMessages, Is.EquivalentTo(errorMessages));
            Assert.That(status.MessagesByErrorLevel(ConfigStatus.ErrorLevel.Error),
                        Is.EquivalentTo(errorMessages));
            Assert.That(status.WarningMessages, Is.EquivalentTo(warningMessages));
            Assert.That(status.MessagesByErrorLevel(ConfigStatus.ErrorLevel.Warning),
                        Is.EquivalentTo(warningMessages));
            Assert.That(status.MessagesByErrorLevel(ConfigStatus.ErrorLevel.Ok), Is.Null);
        }

        [TestCase(ConfigStatus.ErrorLevel.Error, TestName = "ToErrorLevel")]
        [TestCase(ConfigStatus.ErrorLevel.Warning, TestName = "ToWarningLevel")]
        [TestCase(ConfigStatus.ErrorLevel.Ok, TestName = "ToOkLevel")]
        public void AppendErrorTest(ConfigStatus.ErrorLevel initialLevel)
        {
            var newLevel = ConfigStatus.ErrorLevel.Error;
            string initialMessage = "Message init";
            string errorMessage = "Error message";

            ConfigStatus status = StatusBySeverityLevel(initialLevel, initialMessage);
            status.AppendError(errorMessage);
            Assert.That(status.SeverityLevel, Is.EqualTo(newLevel));
            Assert.That(status.IsOk, Is.EqualTo(false));
            Assert.That(status.IsWarningLevel, Is.EqualTo(false));
            Assert.That(status.IsErrorLevel, Is.EqualTo(true));
            string[] errorMessages = GetMessagesForLevel(ConfigStatus.ErrorLevel.Error,
                  new[] { initialLevel, ConfigStatus.ErrorLevel.Error },
                  new[] { initialMessage, errorMessage });
            string[] warningMessages = GetMessagesForLevel(ConfigStatus.ErrorLevel.Warning,
                  new[] { initialLevel, ConfigStatus.ErrorLevel.Error },
                  new[] { initialMessage, errorMessage });
            Assert.That(status.AllMessages,
                        Is.EquivalentTo(errorMessages.Concat(warningMessages)));
            Assert.That(status.ErrorMessages, Is.EquivalentTo(errorMessages));
            Assert.That(status.MessagesByErrorLevel(ConfigStatus.ErrorLevel.Error),
                        Is.EquivalentTo(errorMessages));
            Assert.That(status.WarningMessages, Is.EquivalentTo(warningMessages));
            Assert.That(status.MessagesByErrorLevel(ConfigStatus.ErrorLevel.Warning),
                        Is.EquivalentTo(warningMessages));
            Assert.That(status.MessagesByErrorLevel(ConfigStatus.ErrorLevel.Ok), Is.Null);
        }

        [Test]
        public void MergeTest()
        {
            for (int i = 0; i < _sortedLevels.Length; ++i)
            {
                for (int j = 0; j < _sortedLevels.Length; ++j)
                {
                    string message1 = "Message 1";
                    string message2 = "Message 2";
                    ConfigStatus status1 = StatusBySeverityLevel(_sortedLevels[i], message1);
                    ConfigStatus status2 = StatusBySeverityLevel(_sortedLevels[j], message2);
                    ConfigStatus status1Copy = StatusBySeverityLevel(_sortedLevels[i], message1);
                    ConfigStatus status2Copy = StatusBySeverityLevel(_sortedLevels[j], message2);

                    ConfigStatus merged = status1.Merge(status2);

                    AssertStatusesEqual(status1, status1Copy);
                    AssertStatusesEqual(status2, status2Copy);
                    int maxLevel = Math.Max(i, j);
                    Assert.That(merged.IsOk, Is.EqualTo(IsOk(_sortedLevels[maxLevel])));
                    Assert.That(merged.IsWarningLevel,
                                Is.EqualTo(IsWarning(_sortedLevels[maxLevel])));
                    Assert.That(merged.IsErrorLevel, Is.EqualTo(IsError(_sortedLevels[maxLevel])));
                    string[] errorMessages = GetMessagesForLevel(ConfigStatus.ErrorLevel.Error,
                          new[] {  _sortedLevels[i],  _sortedLevels[j] },
                          new[] { message1, message2 });
                    string[] warningMessages = GetMessagesForLevel(ConfigStatus.ErrorLevel.Warning,
                          new[] { _sortedLevels[i], _sortedLevels[j] },
                          new[] { message1, message2 });

                    Assert.That(merged.AllMessages,
                                Is.EquivalentTo(errorMessages.Concat(warningMessages)));
                    Assert.That(merged.ErrorMessages, Is.EquivalentTo(errorMessages));
                    Assert.That(merged.MessagesByErrorLevel(ConfigStatus.ErrorLevel.Error),
                                Is.EquivalentTo(errorMessages));
                    Assert.That(merged.WarningMessages, Is.EquivalentTo(warningMessages));
                    Assert.That(merged.MessagesByErrorLevel(ConfigStatus.ErrorLevel.Warning),
                                Is.EquivalentTo(warningMessages));
                    Assert.That(merged.MessagesByErrorLevel(ConfigStatus.ErrorLevel.Ok), Is.Null);
                    Assert.That(merged.SeverityLevel, Is.EqualTo(_sortedLevels[maxLevel]));
                }
            }
        }

        string[] GetMessagesForLevel(ConfigStatus.ErrorLevel level,
                                     ConfigStatus.ErrorLevel[] levels, string[] messages)
        {
            if (level == ConfigStatus.ErrorLevel.Ok) return new string[0];
            var res = new List<string>();
            for (var i = 0; i < levels.Length; ++i)
            {
                if (levels[i] == level)
                {
                    res.Add(messages[i]);
                }
            }

            return res.ToArray();
        }

        bool IsOk(ConfigStatus.ErrorLevel level) => level == ConfigStatus.ErrorLevel.Ok;

        bool IsWarning(ConfigStatus.ErrorLevel level) => level == ConfigStatus.ErrorLevel.Warning;

        bool IsError(ConfigStatus.ErrorLevel level) => level == ConfigStatus.ErrorLevel.Error;

        ConfigStatus StatusBySeverityLevel(ConfigStatus.ErrorLevel level, string message)
        {
            switch (level)
            {
                case ConfigStatus.ErrorLevel.Ok:
                    return ConfigStatus.OkStatus();
                case ConfigStatus.ErrorLevel.Warning:
                    return ConfigStatus.WarningStatus(message);
                case ConfigStatus.ErrorLevel.Error:
                    return ConfigStatus.ErrorStatus(message);
                default:
                    throw new ArgumentOutOfRangeException(nameof(level), level,
                              "Unsupported error level value received.");
            }
        }

        void AssertStatusesEqual(ConfigStatus actual, ConfigStatus expected)
        {
            Type t = typeof(ConfigStatus);
            IEnumerable<PropertyInfo> properties =
                t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (PropertyInfo prop in properties)
            {
                object actualVal = prop.GetValue(actual);
                object expectedVal = prop.GetValue(expected);
                Assert.That(actualVal, Is.EqualTo(expectedVal));
            }
        }
    }
}

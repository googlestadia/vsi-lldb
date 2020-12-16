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

using System.Collections.Generic;
using System.Linq;

namespace YetiCommon.Cloud
{
    public class ConfigStatus
    {
        bool _hasWarning;

        bool _hasError;

        List<string> _errorMessages = new List<string>();

        List<string> _warningMessages = new List<string>();

        ConfigStatus()
        {
        }

        public string WarningMessage => string.Join("\r\n", _warningMessages);

        public static ConfigStatus OkStatus() =>
            new ConfigStatus();

        public static ConfigStatus WarningStatus(string message) =>
            new ConfigStatus
                { _hasWarning = true, _warningMessages = new List<string> { message } };

        public static ConfigStatus ErrorStatus(string message) =>
            new ConfigStatus { _hasError = true, _errorMessages = new List<string> { message } };

        public bool IsOk => !_hasWarning && !_hasError;

        public bool IsWarningLevel => _hasWarning && !_hasError;

        public bool IsErrorLevel => _hasError;

        public void AppendWarning(string message)
        {
            _warningMessages.Add(message);
            _hasWarning = true;
        }

        public void AppendError(string message)
        {
            _errorMessages.Add(message);
            _hasError = true;
        }

        public ConfigStatus Merge(ConfigStatus otherStatus) =>
            new ConfigStatus
            {
                _hasError = _hasError || otherStatus._hasError,
                _hasWarning = _hasWarning || otherStatus._hasWarning,
                _errorMessages = _errorMessages.Concat(otherStatus._errorMessages).ToList(),
                _warningMessages =
                    _warningMessages.Concat(otherStatus._warningMessages).ToList()
            };
    }
}
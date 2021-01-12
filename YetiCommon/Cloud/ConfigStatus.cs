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

namespace YetiCommon.Cloud
{
    public class ConfigStatus
    {
        public enum ErrorLevel
        {
            Ok = 0,
            Warning = 1,
            Error = 2
        }

        List<string> _errorMessages = new List<string>();

        List<string> _warningMessages = new List<string>();

        ConfigStatus()
        {
        }

        public string WarningMessage => string.Join("\r\n", _warningMessages);

        public string ErrorMessage => string.Join("\r\n", _errorMessages);

        public List<string> WarningMessages => _warningMessages.ToList();

        public List<string> ErrorMessages => _errorMessages.ToList();

        public List<string> AllMessages => _errorMessages.Concat(_warningMessages).ToList();

        public ErrorLevel SeverityLevel
        {
            get
            {
                if (_errorMessages.Any())
                {
                    return ErrorLevel.Error;
                }
                if (_warningMessages.Any())
                {
                    return ErrorLevel.Warning;
                }

                return ErrorLevel.Ok;
            }
        }

        public static ConfigStatus OkStatus() =>
            new ConfigStatus();

        public static ConfigStatus WarningStatus(string message) =>
            new ConfigStatus { _warningMessages = new List<string> { message } };

        public static ConfigStatus ErrorStatus(string message) =>
            new ConfigStatus { _errorMessages = new List<string> { message } };

        public bool IsOk => SeverityLevel == ErrorLevel.Ok;

        public bool IsWarningLevel => SeverityLevel == ErrorLevel.Warning;

        public bool IsErrorLevel => SeverityLevel == ErrorLevel.Error;

        public List<string> MessagesByErrorLevel(ErrorLevel errorLevel)
        {
            switch (errorLevel)
            {
                case ErrorLevel.Error:
                    return ErrorMessages;
                case ErrorLevel.Warning:
                    return WarningMessages;
                case ErrorLevel.Ok:
                    return null;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(errorLevel), errorLevel,
                        "Unexpected error level received.");
            }
        }

        public void AppendWarning(string message)
        {
            _warningMessages.Add(message);
        }

        public void AppendError(string message)
        {
            _errorMessages.Add(message);
        }

        public ConfigStatus Merge(ConfigStatus otherStatus) =>
            new ConfigStatus
            {
                _errorMessages = _errorMessages.Concat(otherStatus._errorMessages).ToList(),
                _warningMessages =
                    _warningMessages.Concat(otherStatus._warningMessages).ToList()
            };
    }
}
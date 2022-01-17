// Copyright 2021 Google LLC
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
using System.Diagnostics;
using System.Linq;
using YetiVSI;

namespace YetiVSITestsCommon
{
    /// <summary>
    /// This fake simply records and logs the messages and throws DialogException on errors and
    /// warnings. The messages can be retrieved later. Optionally, yes/no questions can be handled
    /// with predefined answers.
    /// </summary>
    public class DialogUtilFake : IDialogUtil
    {
        public class DialogException : Exception
        {
            public DialogException(string message) : base(message)
            {
            }
        }

        public enum MessageType
        {
            Error,
            Warning,
            Info,
            AnsweredYesNo,
            UnansweredYesNo,
        }

        public class Message
        {
            public MessageType MessageType { get; set; }
            public string Text { get; set; }
            public string Details { get; set; } // details or caption
            public string StackTrace { get; set; }

            public override string ToString() =>
                $"{MessageType} {Text}\n\n{Details}\n\n{StackTrace}";
        }

        public class ConfiguredResponse
        {
            public ConfiguredResponse(string questionFragment, bool isYesResponse)
            {
                QuestionFragment = questionFragment;
                IsYesResponse = isYesResponse;
            }

            public string QuestionFragment { get; }
            public bool IsYesResponse { get; }
        }

        readonly List<Message> _messages = new List<Message>();
        readonly List<ConfiguredResponse> _configuredResponses;
        readonly List<string> _silencedWarningFragments;

        public DialogUtilFake(List<ConfiguredResponse> configuredResponses = null,
                              List<string> silencedWarningFragments = null)
        {
            _configuredResponses = configuredResponses;
            _silencedWarningFragments = silencedWarningFragments;
        }

        public void ShowMessage(string message) => RecordMessage(message, null, MessageType.Info);

        public void ShowWarning(string message)
        {
            if (_silencedWarningFragments.Exists(message.Contains))
            {
                return;
            }

            RecordMessage(message, null, MessageType.Warning);
            Trace.WriteLine($"{message}");
        }

        public void ShowWarning(string message, Exception e)
        {
            if (_silencedWarningFragments.Exists(message.Contains))
            {
                return;
            }

            e = e.Demystify();
            RecordMessage(message, e.ToString(), MessageType.Warning);
            Trace.WriteLine($"{message} \n\n {e}");
        }

        public bool ShowOkNoMoreDisplayWarning(string message, string[] settingPath)
        {
            if (_silencedWarningFragments.Exists(message.Contains))
            {
                return false;
            }

            RecordMessage(message, null, MessageType.Warning);
            Trace.WriteLine(message);
            return true;
        }

        public bool ShowOkNoMoreWithDocumentationDisplayWarning(string message,
                                                                string documentationLink,
                                                                string documentationText,
                                                                string[] settingPath)
        {
            return ShowOkNoMoreDisplayWarning(message, settingPath);
        }

        public void ShowError(string message)
        {
            RecordMessage(message, null, MessageType.Error);
            string text = $"{message}";
            Trace.WriteLine(text);
            throw new DialogException(text);
        }

        public void ShowError(string message, Exception e)
        {
            e = e.Demystify();
            RecordMessage(message, e.ToString(), MessageType.Error);
            string text = $"{message} \n\n {e}";
            Trace.WriteLine(text);
            throw new DialogException(text);
        }

        public void ShowErrorWithDetails(string message, string details)
        {
            RecordMessage(message, details, MessageType.Error);
            string text = $"{message} \n\n {details}";
            Trace.WriteLine(text);
            throw new DialogException(text);
        }

        public bool ShowYesNo(string message, string caption)
        {
            ConfiguredResponse response = _configuredResponses.FirstOrDefault(
                r => message.Contains(r.QuestionFragment) ||
                    caption?.Contains(r.QuestionFragment) == true);
            if (response != null)
            {
                RecordMessage(message, caption, MessageType.AnsweredYesNo);
                return response.IsYesResponse;
            }

            // Consider adding a ConfiguredResponse if this happens.
            string text = $"ERROR: Cannot answer yes/no question \n\n {caption} \n\n {message}";
            RecordMessage(message, caption, MessageType.UnansweredYesNo);
            Trace.WriteLine(text);
            throw new DialogException(text);
        }

        public bool ShowYesNoWarning(string message, string caption)
        {
            return ShowYesNo(message, caption);
        }

        public IEnumerable<Message> Messages => _messages;

        void RecordMessage(string message, string details, MessageType type)
        {
            _messages.Add(new Message()
            {
                Text = message,
                Details = details,
                MessageType = type,
                StackTrace = new StackTrace(1, true).ToString()
            });
        }

        public void ShowMessage(string message, string caption)
        {
            RecordMessage(message, null, MessageType.Info);
            Trace.WriteLine(message);
        }
    }
}
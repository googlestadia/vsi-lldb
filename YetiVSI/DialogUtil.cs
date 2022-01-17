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
using System.Diagnostics;
using System.Windows.Forms;

namespace YetiVSI
{
    // Creates dialogs.
    public interface IDialogUtil
    {
        /// <summary>
        /// Displays the provided message as an 'Info' dialog.
        /// </summary>
        void ShowMessage(string message);

        /// <summary>
        /// Displays the provided message as an 'Info' dialog.
        /// </summary>
        void ShowMessage(string message, string caption);

        /// <summary>
        /// Displays a Yes/No prompt.  Returns true if Yes is selected.
        /// </summary>
        /// <returns>true if 'Yes' selected, otherwise false.</returns>
        bool ShowYesNo(string message, string caption);

        /// <summary>
        /// Displays a Yes/No prompt with a warning icon.  Returns true if Yes is selected.
        /// </summary>
        /// <returns>true if 'Yes' selected, otherwise false.</returns>
        bool ShowYesNoWarning(string message, string caption);

        /// <summary>
        /// Displays an error dialog.
        /// </summary>
        void ShowError(string message);

        /// <summary>
        /// Displays an error dialog with an exception stacktrace.
        /// </summary>
        void ShowError(string message, Exception e);

        /// <summary>
        /// Displays an error dialog with a detailed message.
        /// </summary>
        void ShowErrorWithDetails(string message, string details);

        /// <summary>
        ///  Displays a warning dialog.
        /// </summary>
        void ShowWarning(string message);

        /// <summary>
        /// Displays a warning dialog with an exception stacktrace.
        /// </summary>
        void ShowWarning(string message, Exception e);

        /// <summary>
        /// Displays a warning dialog with 'I don't want to see this' button
        /// and with an optional details section.
        /// </summary>
        /// <param name="message">Message to show.</param>
        /// <param name="settingPath">A path to the setting,
        /// where presence of this message can be edited.</param>
        /// <returns>false if 'I don't want to see this' selected, otherwise true.</returns>
        bool ShowOkNoMoreDisplayWarning(string message, string[] settingPath);

        /// <summary>
        /// Displays the same dialog as method ShowOkNoMoreDisplayWarning with additional link to
        /// documentation.
        /// </summary>
        /// <param name="message">Message to show.</param>
        /// <param name="settingPath">A path to the setting,
        /// where presence of this message can be edited.</param>
        /// <param name="documentationLink">Link to the documentation page.</param>
        /// <param name="documentationText">Link text.</param>
        /// <returns>false if 'I don't want to see this' selected, otherwise true.</returns>
        bool ShowOkNoMoreWithDocumentationDisplayWarning(string message, string documentationLink,
                                                         string documentationText,
                                                         string[] settingPath);
    }

    public class DialogUtil : IDialogUtil
    {
        public void ShowMessage(string message)
        {
            MessageBox.Show(message);
        }

        public void ShowMessage(string message, string caption)
        {
            MessageBox.Show(message, caption);
        }

        public bool ShowYesNo(string message, string caption)
        {
            var result = MessageBox.Show(message, caption, MessageBoxButtons.YesNo);
            return result == DialogResult.Yes;
        }

        public bool ShowYesNoWarning(string message, string caption)
        {
            var result = MessageBox.Show(message, caption, MessageBoxButtons.YesNo,
                                         MessageBoxIcon.Warning);
            return result == DialogResult.Yes;
        }

        public void ShowError(string message)
        {
            ShowDetailsDialog(ErrorStrings.DialogTitleError, message, null);
        }

        public void ShowError(string message, Exception e)
        {
            ShowDetailsDialog(ErrorStrings.DialogTitleError, message, e.Demystify().ToString());
        }

        public void ShowErrorWithDetails(string message, string details)
        {
            ShowDetailsDialog(ErrorStrings.DialogTitleError, message, details);
        }

        public void ShowWarning(string message)
        {
            ShowDetailsDialog(ErrorStrings.DialogTitleWarning, message, null);
        }

        public void ShowWarning(string message, Exception e)
        {
            ShowDetailsDialog(ErrorStrings.DialogTitleWarning, message, e.Demystify().ToString());
        }

        public bool ShowOkNoMoreWithDocumentationDisplayWarning(
            string message, string documentationLink, string documentationText,
            string[] settingPath) =>
            ShowDetailsNoMoreDisplayDialog(
                ErrorStrings.DialogTitleWarning, message, documentationLink, documentationText,
                settingPath);

        public bool ShowOkNoMoreDisplayWarning(string message, string[] settingPath) =>
            ShowDetailsNoMoreDisplayDialog(ErrorStrings.DialogTitleWarning, message, string.Empty,
                                           string.Empty, settingPath);

        static void ShowDetailsDialog(string title, string message, string details)
        {
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                var dialog = new DetailsDialog(title, message, details, false);
                dialog.ShowModal();
            }
            else
            {
                Debug.Assert(false);
                Trace.WriteLine("Can not show error dialog on current thread. " +
                                $"{Environment.NewLine}Error dialog message: {message}" +
                                $"{Environment.NewLine}Error dialog details: {details}");
            }
        }

        static bool ShowDetailsNoMoreDisplayDialog(string title, string message,
                                                   string documentationLink,
                                                   string documentationText,
                                                   string[] settingPath)
        {
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                var dialog = new DetailsDialog(title, message, null, true, documentationLink,
                                               documentationText, settingPath);
                dialog.ShowModal();
                return dialog.DialogResult == true;
            }
            else
            {
                Debug.Assert(false);
                Trace.WriteLine("Can not show error dialog on current thread. " +
                                $"{Environment.NewLine}Error dialog message: {message}");
                return true;
            }
        }
    }
}

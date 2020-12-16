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
        // Displays the provided message as an 'Info' dialog.
        void ShowMessage(string message);

        // Displays a Yes/No prompt.  Returns true if Yes is selected.
        bool ShowYesNo(string message, string caption);

        // Displays a Yes/No prompt with a warning icon.  Returns true if Yes is selected.
        bool ShowYesNoWarning(string message, string caption);

        // Displays an error dialog, with an optional details section.
        void ShowError(string message, string details = null);

        // Displays a warning dialog, with an optional details section.
        void ShowWarning(string message, string details = null);
    }

    public class DialogUtil : IDialogUtil
    {
        public void ShowMessage(string message)
        {
            MessageBox.Show(message);
        }

        public bool ShowYesNo(string message, string caption)
        {
            var result = MessageBox.Show(message, caption, MessageBoxButtons.YesNo);
            return result == DialogResult.Yes ? true : false;
        }

        public bool ShowYesNoWarning(string message, string caption)
        {
            var result = MessageBox.Show(message, caption, MessageBoxButtons.YesNo,
                                         MessageBoxIcon.Warning);
            return result == DialogResult.Yes ? true : false;
        }

        public void ShowError(string message, string details = null)
        {
            ShowDetailsDialog(ErrorStrings.DialogTitleError, message, details);
        }

        public void ShowWarning(string message, string details = null)
        {
            ShowDetailsDialog(ErrorStrings.DialogTitleWarning, message, details);
        }

        static void ShowDetailsDialog(string title, string message, string details)
        {
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                var dialog = new ErrorDialog(title, message, details);
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
    }
}

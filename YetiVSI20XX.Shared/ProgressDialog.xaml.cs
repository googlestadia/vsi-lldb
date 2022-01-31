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

﻿using Microsoft.VisualStudio.PlatformUI;
using System;

namespace YetiVSI
{
    /// <summary>
    /// Dialog which can be used to show the current status of a task to the user, and can be
    /// canceled by the user. All methods can be assumed to require the main thread.
    /// </summary>
    public interface IProgressDialog
    {
        /// <summary>
        /// The progress message currently displayed to the user.
        /// </summary>
        string Message { get; set; }

        /// <summary>
        /// Displays the progress dialog and blocks until the dialog is closed.
        /// </summary>
        /// <returns>
        /// True if the dialog is closed because it was marked complete; false if the dialog was
        /// closed or canceled by the user.
        /// </returns>
        bool ShowModal();

        /// <summary>
        /// Closes the progress dialog and marks it as complete.
        /// </summary>
        void Complete();
    }

    /// <summary>
    /// A window that implements IProgressDialog.
    /// </summary>
    public partial class ProgressDialog : DialogWindow, IProgressDialog
    {
        public class Factory
        {
            public virtual IProgressDialog Create(string title, string text)
                => new ProgressDialog(title, text);
        }

        public string Message
        {
            get { return message.Text; }
            set { message.Text = value; }
        }

        private bool opened = false;
        private bool completed = false;

        public ProgressDialog(string title, string text)
        {
            InitializeComponent();

            Title = title;
            description.Text = text;
            ContentRendered += OnContentRendered;
        }

        bool IProgressDialog.ShowModal()
        {
            ShowModal();
            return completed;
        }

        public void Complete()
        {
            completed = true;
            if (opened) { Close(); }
        }

        private void OnContentRendered(object sender, EventArgs e)
        {
            opened = true;
            if (completed) { Close(); }
        }
    }
}

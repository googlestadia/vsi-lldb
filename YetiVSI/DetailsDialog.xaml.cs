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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using YetiCommon.Logging;

namespace YetiVSI
{
    public partial class DetailsDialog
    {
        readonly string _details;
        readonly string _documentationLink;

        public DetailsDialog(string title, string message, string details)
            : this(title, message, details, null, null, null)
        {
        }

        public DetailsDialog(string title, string message, string documentationLink,
                             string documentationText, string[] dontShowAgainSettingPath)
            : this(title, message, null, documentationLink, documentationText,
                   dontShowAgainSettingPath)
        {
        }

        private DetailsDialog(string title, string message, string details,
                              string documentationLink, string documentationText,
                              string[] dontShowAgainSettingPath)
        {
            _details = details;
            _documentationLink = documentationLink;

            InitializeComponent();

            Title = title;
            Message.Text = message;

            // Handle details.
            if (details != null)
            {
                Details.Text = details;
                DetailsExpander.Visibility = Visibility.Visible;
            }
            else
            {
                DetailsExpander.Visibility = Visibility.Hidden;
            }

            // Handle documentation text and link.
            if (!string.IsNullOrEmpty(documentationLink) &&
                !string.IsNullOrEmpty(documentationText))
            {
                DocumentationBlock.Visibility = Visibility.Visible;
                DocumentationText.Text = documentationText;
            }
            else
            {
                DocumentationBlock.Visibility = Visibility.Hidden;
            }

            // Handle the "don't show again" button.
            if (dontShowAgainSettingPath != null)
            {
                DontShowAgain.Visibility = Visibility.Visible;
                DontShowAgain.ToolTip =
                    ErrorStrings.DontShowAgainSettingHint(dontShowAgainSettingPath);
            }
            else
            {
                DontShowAgain.Visibility = Visibility.Hidden;
            }
        }

        void DocumentationLinkClick(object sender, RoutedEventArgs e)
        {
            Process.Start(_documentationLink).Dispose();
        }

        void OkClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        void OpenLogs(object sender, RoutedEventArgs e)
        {
            var path = "<Invalid>";
            try
            {
                path = YetiLog.CurrentLogFile;
                if (!File.Exists(path))
                {
                    path = Path.GetDirectoryName(path);
                }

                Process.Start(path).Dispose();
            }
            catch (InvalidOperationException ex)
            {
                Trace.WriteLine($"Open logs failed: {ex.Demystify()}");
                LogsLink.Text = ErrorStrings.FailedToOpenLogsBecauseLoggingNotInitialized;
            }
            catch (Exception ex) when (ex is Win32Exception || ex is FileNotFoundException)
            {
                Trace.WriteLine($"Open logs failed: {ex.Demystify()}");
                LogsLink.Text = ErrorStrings.FailedToOpenLogsBecauseLogFileMayNotExist(path);
            }
        }

        void ReportBug(object sender, RoutedEventArgs e)
        {
            YetiVSI.ReportBug.TriggerCommand(_details);
        }

        void DetailsExpanded(object sender, RoutedEventArgs e)
        {
            SizeToContent = SizeToContent.Manual;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            Width = 525;
            Height = 350;
            SetValue(MaxWidthProperty, DependencyProperty.UnsetValue);
        }

        void DetailsCollapsed(object sender, RoutedEventArgs e)
        {
            MaxWidth = 525;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
        }

        void DoNotShowAgainClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

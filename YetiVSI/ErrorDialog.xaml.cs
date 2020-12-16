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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using YetiCommon;
using YetiCommon.Logging;

namespace YetiVSI
{
    // TODO: rename to something generic like DetailsDialog.
    public partial class ErrorDialog : DialogWindow
    {
        public ErrorDialog(string title, string message, string details)
        {
            InitializeComponent();
            Title = title;
            this.message.Text = message;
            if (!string.IsNullOrEmpty(details))
            {
                this.details.Text = details;
            }
            else
            {
                detailsExpander.Visibility = Visibility.Collapsed;
            }
        }

        private void okClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void openLogs(object sender, RoutedEventArgs e)
        {
            var path = "<Invalid>";
            try
            {
                path = YetiLog.CurrentLogFile;
                if (!File.Exists(path))
                {
                    path = Path.GetDirectoryName(path);
                }

                Process.Start(path);
            }
            catch (InvalidOperationException ex)
            {
                Trace.WriteLine(ex);
                logsLink.Text = ErrorStrings.FailedToOpenLogsBecauseLoggingNotInitialized;
            }
            catch (Exception ex) when (ex is Win32Exception || ex is FileNotFoundException)
            {
                Trace.WriteLine(ex);
                logsLink.Text = ErrorStrings.FailedToOpenLogsBecauseLogFileMayNotExist(path);
            }
        }

        private void detailsExpanded(object sender, RoutedEventArgs e)
        {
            SizeToContent = SizeToContent.Manual;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            Width = 525;
            Height = 350;
            SetValue(MaxWidthProperty, DependencyProperty.UnsetValue);
        }

        private void detailsCollapsed(object sender, RoutedEventArgs e)
        {
            MaxWidth = 525;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
        }
    }
}

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

using GgpGrpc.Models;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace YetiVSI
{
    // Uses a window to allow users to choose from a list of instances.
    public interface IInstanceSelectionWindow
    {
        // Displays the window and blocks until the user performs a selection. Returns the selected
        // instance or null if they cancelled.
        Gamelet Run();
    }

    public partial class InstanceSelectionWindow : IInstanceSelectionWindow
    {
        public class Factory
        {
            public virtual IInstanceSelectionWindow Create(
                List<Gamelet> instances) => new InstanceSelectionWindow(instances);
        }

        Gamelet _selected;

        InstanceSelectionWindow(List<Gamelet> instances)
        {
            InitializeComponent();
            InstanceList.ItemsSource = instances;
            InstanceList.Focus();

            // Force sorting by the Name column (DisplayName property) in ascending order.
            var dataView = CollectionViewSource.GetDefaultView(InstanceList.ItemsSource);
            dataView.SortDescriptions.Add(
                new SortDescription("DisplayName", ListSortDirection.Ascending));
        }

        public Gamelet Run()
        {
            ShowModal();
            return _selected;
        }

        void InstanceListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectButton.IsEnabled = InstanceList.SelectedItem != null;
        }

        void CancelClick(object sender, RoutedEventArgs e)
        {
            Cancel();
        }

        void SelectClick(object sender, RoutedEventArgs e)
        {
            TryAccept();
        }

        void InstancesList_MouseDoubleClick(object sender,
                                            System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                e.Handled = true;
                TryAccept();
            }
        }

        void DialogWindow_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                e.Handled = true;
                TryAccept();
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                Cancel();
            }
        }

        void TryAccept()
        {
            _selected = (InstanceList.SelectedItem as Gamelet);
            if (_selected != null)
            {
                Close();
            }
        }

        void Cancel()
        {
            _selected = null;
            Close();
        }

        void DialogWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (InstanceList.HasItems)
            {
                InstanceList.SelectedIndex = 0;
            }
        }
    }
}
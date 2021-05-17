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
using System.Windows;
using System.Windows.Controls;

namespace YetiVSI
{
    public partial class ProjectInstanceSelection : IInstanceSelectionWindow
    {
        public class Factory
        {
            public virtual IInstanceSelectionWindow Create(
                List<Gamelet> instances) => new ProjectInstanceSelection(instances);
        }

        Gamelet _selected;

        ProjectInstanceSelection(List<Gamelet> instances)
        {
            InitializeComponent();
            InstancesList.ItemsSource = instances;
            InstancesList.Focus();
        }

        public Gamelet Run()
        {
            ShowModal();
            return _selected;
        }

        void InstancesListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectButton.IsEnabled = InstancesList.SelectedItem != null;
        }

        void CancelClick(object sender, RoutedEventArgs e)
        {
            Cancel();
        }

        void SelectClick(object sender, RoutedEventArgs e)
        {
            TryAccept();
        }

        void InstanceList_MouseDoubleClick(object sender,
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
            _selected = (InstancesList.SelectedItem as Gamelet);
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
            if (InstancesList.HasItems)
            {
                InstancesList.SelectedIndex = 0;
            }
        }
    }
}
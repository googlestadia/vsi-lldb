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
using Microsoft.VisualStudio.PlatformUI;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace YetiVSI
{
    // Uses a window to allow users to choose from a list of gamelets.
    public interface IGameletSelectionWindow
    {
        // Displays the window and blocks until the user performs a selection. Returns the selected
        // gamelet or null if they cancelled.
        Gamelet Run();
    }

    public partial class GameletSelectionWindow : DialogWindow, IGameletSelectionWindow
    {
        public class Factory
        {
            public virtual IGameletSelectionWindow Create(List<Gamelet> gamelets)
            {
                return new GameletSelectionWindow(gamelets);
            }
        }

        Gamelet selected;

        private GameletSelectionWindow(List<Gamelet> gamelets)
        {
            InitializeComponent();
            gameletList.ItemsSource = gamelets;
            gameletList.Focus();

            // Force sorting by the Name column (DisplayName property) in ascending order.
            var dataView = CollectionViewSource.GetDefaultView(gameletList.ItemsSource);
            dataView.SortDescriptions.Add(
                new SortDescription("DisplayName", ListSortDirection.Ascending));
        }

        public Gamelet Run()
        {
            ShowModal();
            return selected;
        }

        private void gameletListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectButton.IsEnabled = gameletList.SelectedItem != null;
        }

        private void cancelClick(object sender, RoutedEventArgs e)
        {
            Cancel();
        }

        private void selectClick(object sender, RoutedEventArgs e)
        {
            TryAccept();
        }

        private void gameletList_MouseDoubleClick(object sender,
                                                  System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                e.Handled = true;
                TryAccept();
            }
        }

        private void DialogWindow_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
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

        private bool TryAccept()
        {
            selected = (gameletList.SelectedItem as Gamelet);
            if (selected != null)
            {
                Close();
                return true;
            }

            return false;
        }

        private void Cancel()
        {
            selected = null;
            Close();
        }

        private void DialogWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (gameletList.HasItems)
            {
                gameletList.SelectedIndex = 0;
            }
        }
    }
}
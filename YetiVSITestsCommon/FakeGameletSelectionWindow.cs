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
using YetiVSI;

namespace YetiVSITestsCommon
{
    // Fake selection "window" that doesn't actually pop up a gamelet selection
    // window, but just picks the first gamelet. Useful for tests where UI is no.
    public class FakeInstanceSelectionWindow : IInstanceSelectionWindow
    {
        public class Factory : InstanceSelectionWindow.Factory
        {
            public Factory()
            {
            }

            public override IInstanceSelectionWindow Create(List<Gamelet> gamelets) =>
                new FakeInstanceSelectionWindow(gamelets);
        }

        readonly List<Gamelet> _gamelets;

        public FakeInstanceSelectionWindow(List<Gamelet> gamelets)
        {
            _gamelets = gamelets;
        }

        // IGameletSelectionWindow:
        public Gamelet Run() => _gamelets[0];
    }
}
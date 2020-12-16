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
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using YetiCommon;

namespace YetiVSI.CoreAttach
{
    // A menu command to attach to core dumps.
    internal sealed class CoreAttachCommand
    {
        readonly Package package;
        readonly IDialogUtil dialogUtil;
        readonly ServiceManager serviceManager;

        public static CoreAttachCommand Register(Package package)
        {
            return new CoreAttachCommand(package);
        }

        private CoreAttachCommand(Package package)
        {
            this.package = package;
            dialogUtil = new DialogUtil();
            serviceManager = new ServiceManager();

            ((package as IServiceProvider)
                .GetService(typeof(IMenuCommandService)) as OleMenuCommandService)?.AddCommand(
                    new MenuCommand(HandleCoreAttachCommand, new CommandID(
                        YetiConstants.CommandSetGuid,
                        PkgCmdID.cmdidCrashDumpAttachCommand)));
        }

        private void HandleCoreAttachCommand(object sender, EventArgs e)
        {
            var window = new CoreAttachWindow(package);
            window.ShowModal();
        }
    }
}

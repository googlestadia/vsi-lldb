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

namespace YetiCommon
{
    // Handles retreiving credentails from disk, or from user-specified options.
    public interface ICredentialManager : GgpGrpc.ICredentialManager
    {
    }

    // Loads an option containing a user-specified account to use.
    public interface IAccountOptionLoader
    {
        // Returns the user-configured account name.
        string LoadAccountOption();
    }

    public class CredentialManager : ICredentialManager
    {
        CredentialConfig.Factory credentialConfigFactory;
        IAccountOptionLoader accountOptionLoader;

        public CredentialManager(CredentialConfig.Factory credentialConfigFactory)
            : this(credentialConfigFactory, null) { }

        public CredentialManager(CredentialConfig.Factory credentialConfigFactory,
            IAccountOptionLoader accountOptionLoader)
        {
            this.credentialConfigFactory = credentialConfigFactory;
            this.accountOptionLoader = accountOptionLoader;
        }

        public string LoadAccount()
        {
            var account = accountOptionLoader?.LoadAccountOption();
            if (!string.IsNullOrEmpty(account))
            {
                return account;
            }
            account = credentialConfigFactory.LoadOrDefault().DefaultAccount;
            if (account == null)
            {
                return "";
            }
            return account;
        }
    }
}
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

using System.Collections.Generic;
using System.Linq;
using YetiCommon;

namespace YetiVSI.DebugEngine
{
    public class LinuxSignals
    {
        public static IReadOnlyDictionary<int, Signal> GetDefaultSignalsMap()
        {
            return DEFAULT_SIGNALS_MAP;
        }

        private static readonly IReadOnlyList<Signal> DEFAULT_SIGNALS_LIST = new List<Signal>
        {
            new Signal { name = "SIGHUP", code = 1, stop = true},
            new Signal { name = "SIGINT", code = 2, stop = true},
            new Signal { name = "SIGQUIT", code = 3, stop = true},
            new Signal { name = "SIGILL", code = 4, stop = true},
            new Signal { name = "SIGTRAP", code = 5, stop = true},
            new Signal { name = "SIGABRT", code = 6, stop = true,
                alias = new List<string> { "SIGIOT" } },
            new Signal { name = "SIGBUS", code = 7, stop = true},
            new Signal { name = "SIGFPE", code = 8, stop = true},
            new Signal { name = "SIGKILL", code = 9, stop = true},
            new Signal { name = "SIGUSR1", code = 10, stop = true},
            new Signal { name = "SIGSEGV", code = 11, stop = true},
            new Signal { name = "SIGUSR2", code = 12, stop = true},
            new Signal { name = "SIGPIPE", code = 13, stop = false},
            new Signal { name = "SIGALRM", code = 14, stop = false},
            new Signal { name = "SIGTERM", code = 15, stop = true},
            new Signal { name = "SIGSTKFLT", code = 16, stop = true},
            new Signal { name = "SIGCHLD", code = 17, stop = false,
                alias = new List<string> { "SIGCLD" } },
            new Signal { name = "SIGCONT", code = 18, stop = true},
            new Signal { name = "SIGSTOP", code = 19, stop = true},
            new Signal { name = "SIGSTP", code = 20, stop = true},
            new Signal { name = "SIGTTIN", code = 21, stop = true},
            new Signal { name = "SIGTTOU", code = 22, stop = true},
            new Signal { name = "SIGURG", code = 23, stop = true},
            new Signal { name = "SIGXCPU", code = 24, stop = true},
            new Signal { name = "SIGXFSZ", code = 25, stop = true},
            new Signal { name = "SIGVTALRM", code = 26, stop = true},
            new Signal { name = "SIGPROF", code = 27, stop = false},
            new Signal { name = "SIGWINCH", code = 28, stop = true},
            new Signal { name = "SIGIO", code = 29, stop = true,
                alias = new List<string> { "SIGPOLL" } },
            new Signal { name = "SIGPWR", code = 30, stop = true},
            new Signal { name = "SIGSYS", code = 31, stop = true},
            new Signal { name = "SIG32", code = 32, stop = false},
            new Signal { name = "SIG33", code = 33, stop = false},
            new Signal { name = "SIGTMIN", code = 34, stop = false},
            new Signal { name = "SIGTMIN+1", code = 35, stop = false},
            new Signal { name = "SIGTMIN+2", code = 36, stop = false},
            new Signal { name = "SIGTMIN+3", code = 37, stop = false},
            new Signal { name = "SIGTMIN+4", code = 38, stop = false},
            new Signal { name = "SIGTMIN+5", code = 39, stop = false},
            new Signal { name = "SIGTMIN+6", code = 40, stop = false},
            new Signal { name = "SIGTMIN+7", code = 41, stop = false},
            new Signal { name = "SIGTMIN+8", code = 42, stop = false},
            new Signal { name = "SIGTMIN+9", code = 43, stop = false},
            new Signal { name = "SIGTMIN+10", code = 44, stop = false},
            new Signal { name = "SIGTMIN+11", code = 45, stop = false},
            new Signal { name = "SIGTMIN+12", code = 46, stop = false},
            new Signal { name = "SIGTMIN+13", code = 47, stop = false},
            new Signal { name = "SIGTMIN+14", code = 48, stop = false},
            new Signal { name = "SIGTMIN+15", code = 49, stop = false},
            new Signal { name = "SIGTMAX-14", code = 50, stop = false},
            new Signal { name = "SIGTMAX-13", code = 51, stop = false},
            new Signal { name = "SIGTMAX-12", code = 52, stop = false},
            new Signal { name = "SIGTMAX-11", code = 53, stop = false},
            new Signal { name = "SIGTMAX-10", code = 54, stop = false},
            new Signal { name = "SIGTMAX-9", code = 55, stop = false},
            new Signal { name = "SIGTMAX-8", code = 56, stop = false},
            new Signal { name = "SIGTMAX-7", code = 57, stop = false},
            new Signal { name = "SIGTMAX-6", code = 58, stop = false},
            new Signal { name = "SIGTMAX-5", code = 59, stop = false},
            new Signal { name = "SIGTMAX-4", code = 60, stop = false},
            new Signal { name = "SIGTMAX-3", code = 61, stop = false},
            new Signal { name = "SIGTMAX-2", code = 62, stop = false},
            new Signal { name = "SIGTMAX-1", code = 63, stop = false},
            new Signal { name = "SIGTMAX", code = 64, stop = false},
        };

        private static readonly IReadOnlyDictionary<int, Signal> DEFAULT_SIGNALS_MAP =
            DEFAULT_SIGNALS_LIST.ToDictionary(signal => signal.code, signal => signal);
    }
}

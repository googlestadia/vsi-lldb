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
using System.Collections.Generic;

namespace YetiVSI.DebugEngine
{
    public class SignalMap
    {
        public static readonly IReadOnlyDictionary<ulong, Tuple<string, string>> Map =
            new Dictionary<ulong, Tuple<string, string>>
            {
                {1, new Tuple<string, string>("SIGHUP", "hangup") },
                {2, new Tuple<string, string>("SIGINT", "interrupt") },
                {3, new Tuple<string, string>("SIGQUIT", "quit") },
                {4, new Tuple<string, string>("SIGILL", "illegal instruction") },
                {5, new Tuple<string, string>("SIGTRAP", "trace trap (not reset when caught)") },
                {6, new Tuple<string, string>("SIGABRT", "abort() is called") },
                {7, new Tuple<string, string>("SIGBUS", "bus error") },
                {8, new Tuple<string, string>("SIGFPE", "floating point exception") },
                {9, new Tuple<string, string>("SIGKILL", "kill") },
                {10, new Tuple<string, string>("SIGUSR1", "user defined signal 1") },
                {11, new Tuple<string, string>("SIGSEGV", "segmentation violation") },
                {12, new Tuple<string, string>("SIGUSR2", "user defined signal 2") },
                {13, new Tuple<string, string>(
                    "SIGPIPE", "write on a pipe with no one to read it") },
                {14, new Tuple<string, string>("SIGALRM", "alarm clock") },
                {15, new Tuple<string, string>(
                    "SIGTERM", "software termination signal from kill") },
                {16, new Tuple<string, string>("SIGSTKFLT", "stack fault") },
                {17, new Tuple<string, string>("SIGCHLD", "child status has changed") },
                {18, new Tuple<string, string>("SIGCONT", "process continue") },
                {19, new Tuple<string, string>("SIGSTOP", "process stop") },
                {20, new Tuple<string, string>("SIGTSTP", "tty stop") },
                {21, new Tuple<string, string>("SIGTTIN", "background tty read") },
                {22, new Tuple<string, string>("SIGTTOU", "background tty write") },
                {23, new Tuple<string, string>("SIGURG", "urgent data on socket") },
                {24, new Tuple<string, string>("SIGXCPU", "CPU resource exceeded") },
                {25, new Tuple<string, string>("SIGXFSZ", "file size limit exceeded") },
                {26, new Tuple<string, string>("SIGVTALRM", "virtual time alarm") },
                {27, new Tuple<string, string>("SIGPROF", "profiling time alarm") },
                {28, new Tuple<string, string>("SIGWINCH", "window size changes") },
                {29, new Tuple<string, string>("SIGIO", "input/output ready/Pollable event") },
                {30, new Tuple<string, string>("SIGPWR", "power failure") },
                {31, new Tuple<string, string>("SIGSYS",  "invalid system call") },
                {32, new Tuple<string, string>("SIG32", "threading library internal signal 1") },
                {33, new Tuple<string, string>("SIG33", "threading library internal signal 2") },
                {34, new Tuple<string, string>("SIGRTMIN", "real time signal 0")  },
                {35, new Tuple<string, string>("SIGRTMIN+1", "real time signal 1") },
                {36, new Tuple<string, string>("SIGRTMIN+2", "real time signal 2") },
                {37, new Tuple<string, string>("SIGRTMIN+3", "real time signal 3") },
                {38, new Tuple<string, string>("SIGRTMIN+4", "real time signal 4") },
                {39, new Tuple<string, string>("SIGRTMIN+5", "real time signal 5") },
                {40, new Tuple<string, string>("SIGRTMIN+6", "real time signal 6") },
                {41, new Tuple<string, string>("SIGRTMIN+7", "real time signal 7") },
                {42, new Tuple<string, string>("SIGRTMIN+8", "real time signal 8") },
                {43, new Tuple<string, string>("SIGRTMIN+9", "real time signal 9") },
                {44, new Tuple<string, string>("SIGRTMIN+10", "real time signal 10") },
                {45, new Tuple<string, string>("SIGRTMIN+11", "real time signal 11") },
                {46, new Tuple<string, string>("SIGRTMIN+12", "real time signal 12") },
                {47, new Tuple<string, string>("SIGRTMIN+13", "real time signal 13") },
                {48, new Tuple<string, string>("SIGRTMIN+14", "real time signal 14") },
                {49, new Tuple<string, string>("SIGRTMIN+15", "real time signal 15") },
                {50, new Tuple<string, string>("SIGRTMAX-14", "real time signal 16") },
                {51, new Tuple<string, string>("SIGRTMAX-13", "real time signal 17") },
                {52, new Tuple<string, string>("SIGRTMAX-12", "real time signal 18") },
                {53, new Tuple<string, string>("SIGRTMAX-11", "real time signal 19") },
                {54, new Tuple<string, string>("SIGRTMAX-10", "real time signal 20") },
                {55, new Tuple<string, string>("SIGRTMAX-9", "real time signal 21") },
                {56, new Tuple<string, string>("SIGRTMAX-8", "real time signal 22") },
                {57, new Tuple<string, string>("SIGRTMAX-7", "real time signal 23") },
                {58, new Tuple<string, string>("SIGRTMAX-6", "real time signal 24") },
                {59, new Tuple<string, string>("SIGRTMAX-5", "real time signal 25") },
                {60, new Tuple<string, string>("SIGRTMAX-4", "real time signal 26") },
                {61, new Tuple<string, string>("SIGRTMAX-3", "real time signal 27") },
                {62, new Tuple<string, string>("SIGRTMAX-2", "real time signal 28") },
                {63, new Tuple<string, string>("SIGRTMAX-1", "real time signal 29") },
                {64, new Tuple<string, string>("SIGRTMAX", "real time signal 30") },
            };
    }
}

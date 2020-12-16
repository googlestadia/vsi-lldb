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

using CommandLine;
using System.Collections.Generic;

namespace DebuggerGrpcServer
{
    class CommandLineOptions
    {
        [Option('i', "in", Required = true, Separator = ',',
            HelpText = "Pipe handle strings of the input pipes. Count must match OutPipeHandles.")]
        public IEnumerable<string> InPipeHandles { get; set; }

        [Option('o', "out", Required = true, Separator = ',',
            HelpText = "Pipe handle strings of the output pipes. Count must match InPipeHandles.")]
        public IEnumerable<string> OutPipeHandles { get; set; }

        static public bool TryParse(string[] args, out CommandLineOptions opts)
        {
            ParserResult<CommandLineOptions> result =
                Parser.Default.ParseArguments<CommandLineOptions>(args);

            CommandLineOptions outOpts = null;
            result.WithParsed(inOpts => outOpts = inOpts);
            opts = outOpts;
            return opts != null;
        }
    }
}

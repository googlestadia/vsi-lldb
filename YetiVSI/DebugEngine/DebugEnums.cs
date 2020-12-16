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

using Microsoft.VisualStudio.Debugger.Interop;
using System.Collections.Generic;
using System.Linq;
using YetiVSI.DebugEngine.AsyncOperations;

namespace YetiVSI.DebugEngine
{
    public class ModuleEnumFactory
    {
        public virtual IEnumDebugModules2 Create(IEnumerable<IDebugModule2> modules)
        {
            return new ModulesEnum(modules.ToArray());
        }
    }

    class ModulesEnum : PortSupplier.DebugEnum<IDebugModule2, IEnumDebugModules2>,
        IEnumDebugModules2
    {
        public ModulesEnum(IDebugModule2[] data) : base(data)
        {
        }
    }

    public class ThreadEnumFactory
    {
        public virtual IEnumDebugThreads2 Create(IEnumerable<IDebugThread2> threads)
        {
            return new ThreadsEnum(threads.ToArray());
        }
    }

    class ThreadsEnum : PortSupplier.DebugEnum<IDebugThread2, IEnumDebugThreads2>,
        IEnumDebugThreads2
    {
        public ThreadsEnum(IDebugThread2[] data) : base(data)
        {
        }
    }

    public class BreakpointErrorEnumFactory
    {
        public virtual IEnumDebugErrorBreakpoints2 Create(
            IEnumerable<IDebugErrorBreakpoint2> breakpoints)
        {
            return new BreakpointErrorEnum(breakpoints.ToArray());
        }
    }

    class BreakpointErrorEnum :
        PortSupplier.DebugEnum<IDebugErrorBreakpoint2, IEnumDebugErrorBreakpoints2>,
        IEnumDebugErrorBreakpoints2
    {
        public BreakpointErrorEnum(IDebugErrorBreakpoint2[] data) : base(data)
        {
        }
    }

    public class BoundBreakpointEnumFactory
    {
        public virtual IEnumDebugBoundBreakpoints2 Create(
            IEnumerable<IDebugBoundBreakpoint2> breakpoints)
        {
            return new BoundBreakpointEnum(breakpoints.ToArray());
        }
    }

    class BoundBreakpointEnum :
        PortSupplier.DebugEnum<IDebugBoundBreakpoint2, IEnumDebugBoundBreakpoints2>,
        IEnumDebugBoundBreakpoints2
    {
        public BoundBreakpointEnum(IDebugBoundBreakpoint2[] data) : base(data)
        {
        }
    }

    public class CodeContextEnumFactory
    {
        public virtual IEnumDebugCodeContexts2 Create(IEnumerable<IDebugCodeContext2> codeContexts)
        {
            return new CodeContextEnum(codeContexts.ToArray());
        }
    }

    class CodeContextEnum :
        PortSupplier.DebugEnum<IDebugCodeContext2, IEnumDebugCodeContexts2>,
        IEnumDebugCodeContexts2
    {
        public CodeContextEnum(IDebugCodeContext2[] data) : base(data)
        {
        }
    }

}

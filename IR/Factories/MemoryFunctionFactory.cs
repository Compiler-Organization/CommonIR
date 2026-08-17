using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Factories
{
    internal class MemoryFunctionFactory
    {
        IRModule Module { get; set; }

        public MemoryFunctionFactory(IRModule module) 
        {
            this.Module = module;
        }

        public IRGlobal CreateHeapPointer()
        {
            return this.Module.CreateGlobal("__heap_ptr", new IRType(IRDataTypes.Int32), new IRConstantInteger(IRDataTypes.Int32, 0), isMutable: true);
        }

        public IRFunction CreateMalloc()
        {
            return this.Module.CreateFunction("__malloc", [new IRType(IRDataTypes.Int32)], [new IRLocal("bytes", new IRType(IRDataTypes.Int32), false)], isExport: true);
        }

        public IRFunction CreateFree()
        {
            return this.Module.CreateFunction("__free", [], [new IRLocal(new IRType(IRDataTypes.Int32), false)], isExport: true); ;
        }
    }
}

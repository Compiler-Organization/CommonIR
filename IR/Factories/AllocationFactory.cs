using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Factories
{
    internal class AllocationFactory
    {
        IRModule Module { get; set; }

        public AllocationFactory(IRModule module) 
        {
            this.Module = module;
        }

        public void CreateHeapPointer()
        {
            this.Module.CreateGlobal("__heap_ptr", new IRType(IRDataTypes.Int32), new IRConstantInteger(IRDataTypes.Int32, 0), isMutable: true);
        }

        public IRFunction CreateMalloc()
        {
            IRFunction mallocFunction = this.Module.CreateFunction("__malloc", [new IRType(IRDataTypes.Int32)], [new IRLocal(new IRType(IRDataTypes.Int32), false)], isExport: true);
            IRBuilder builder = new IRBuilder(this.Module, mallocFunction, mallocFunction.Entryblock);
            
            

            return mallocFunction;
        }

        public IRFunction CreateFree()
        {
            IRFunction freeFunction = this.Module.CreateFunction("__free", [], [new IRLocal(new IRType(IRDataTypes.Int32), false)], isExport: true);
            IRBuilder builder = new IRBuilder(this.Module, freeFunction, freeFunction.Entryblock);



            return freeFunction;
        }
    }
}

using CommonIR.IR;
using CommonIR.IR.Factories;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.WASM.Emit
{
    internal class WasmMemoryFunctionEmitter
    {
        IRModule Module { get; set; }

        MemoryFunctionFactory MemoryFunctionFactory { get; set; }

        public WasmMemoryFunctionEmitter(IRModule module)
        {
            this.Module = module;
            this.MemoryFunctionFactory = new MemoryFunctionFactory(module);
        }

        public IRFunction EmitMalloc()
        {
            IRFunction malloc = this.MemoryFunctionFactory.CreateMalloc();
            IRBuilder builder = new IRBuilder(Module, malloc, malloc.Entryblock);
            builder.BuildReturn(builder.BuildConstantInteger(IRDataTypes.Int32, 0));

            return malloc;
        }

        public IRFunction EmitFree()
        {
            IRFunction free = this.MemoryFunctionFactory.CreateFree();



            return free;
        }
    }
}

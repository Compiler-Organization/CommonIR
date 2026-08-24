using CommonIR.IR;
using CommonIR.IR.Factories;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.ControlFlow;
using CommonIR.IR.Grammar.Instructions.Numeric;
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

        // TODO: Calculate the size of data loaded into heap at start, set the heap pointer to the last location.
        public IRGlobal EmitHeapPointer()
        {
            IRGlobal heapPointer = this.MemoryFunctionFactory.CreateHeapPointer();
            heapPointer.InitialValue = new IRConstantInteger(IRDataTypes.Int32, (long)this.Module.ConstantsSize);
            return heapPointer;
        }

        public IRFunction EmitMalloc(IRGlobal heapPointer) // TODO: Impliment an actual malloc, with blocks and all of that.
        {
            IRFunction malloc = this.MemoryFunctionFactory.CreateMalloc();
            IRLocal bytesParameter = malloc.Parameters.First();
            IRBuilder builder = new IRBuilder(Module, malloc, malloc.Entryblock);

            IRLocal currentPtr = malloc.CreateLocal("current_ptr", new IRType(IRDataTypes.Int32), isMutable: true);
            IRLocal nextPtr = malloc.CreateLocal("next_ptr", new IRType(IRDataTypes.Int32), isMutable: true);
            IRLocal memSizeBytes = malloc.CreateLocal("mem_size_bytes", new IRType(IRDataTypes.Int32), isMutable: true);

            builder.BuildStore(currentPtr, heapPointer);
            IRValueInstruction addPtrBytes = builder.BuildAdd(currentPtr, bytesParameter);
            builder.BuildStore(nextPtr, addPtrBytes);

            IRValueInstruction memorySize = (IRValueInstruction)builder.BuildBytes([0x3F, 0x00], new IRType(IRDataTypes.Int32)); // memory.size
            IRValueInstruction pageMaxSize = builder.BuildConstantInteger(IRDataTypes.Int32, 65536);
            IRValueInstruction memoryPages = builder.BuildMultiply(memorySize, pageMaxSize);
            builder.BuildStore(memSizeBytes, memoryPages);

            //IRBlock allocationExceedsBlock = malloc.CreateBlock("allocationExceeds");

            //IRValueInstruction compare = builder.BuildCompare(IRComparisonOperator.GreaterThan, nextPtr, memSizeBytes);
            //builder.BuildConditionalBranch(compare, allocationExceedsBlock);

            //builder.PositionAtStart(malloc, allocationExceedsBlock);
            //IRValueInstruction memoryGrowResult = (IRValueInstruction)builder.BuildBytes([0x41, 0x01, 0x40, 0x00], new IRType(IRDataTypes.Int32)); // i32.const 1, memory.grow
            //IRValueInstruction failed = builder.BuildConstantInteger(IRDataTypes.Int32, -1);

            //IRBlock failedAllocationBlock = malloc.CreateBlock("failedAllocationBlock");
            //IRValueInstruction ifFailedGrow = builder.BuildCompare(IRComparisonOperator.EqualTo, memoryGrowResult, failed);
            //builder.BuildConditionalBranch(ifFailedGrow, failedAllocationBlock);
            //builder.PositionAtStart(malloc, failedAllocationBlock);
            //builder.BuildReturn(builder.BuildConstantInteger(IRDataTypes.Int32, 0));

            //builder.PositionAtEnd(malloc, malloc.Entryblock);

            builder.BuildStore(heapPointer, nextPtr);
            builder.BuildReturn(currentPtr);

            return malloc;
        }

        public IRFunction EmitFree(IRGlobal heapPointer)
        {
            IRFunction free = this.MemoryFunctionFactory.CreateFree();



            return free;
        }
    }
}

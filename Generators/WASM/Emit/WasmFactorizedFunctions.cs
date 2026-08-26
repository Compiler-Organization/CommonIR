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
    internal class WasmFactorizedFunctions
    {
        IRModule Module { get; set; }

        FunctionFactory FunctionFactory { get; set; }

        public IRGlobal HeapPointer { get; set; }
        public IRFunction Malloc { get; set; }
        public IRFunction Free { get; set; }
        public IRFunction Panic { get; set; }

        public WasmFactorizedFunctions(IRModule module)
        {
            this.Module = module;
            this.FunctionFactory = new FunctionFactory(module);

            this.HeapPointer = EmitHeapPointer();
            this.Malloc = EmitMalloc(this.HeapPointer);
            this.Free = EmitFree(this.HeapPointer);
            this.Panic = EmitPanic();
        }

        IRGlobal EmitHeapPointer()
        {
            IRGlobal heapPointer = this.FunctionFactory.CreateHeapPointer();
            heapPointer.InitialValue = new IRConstantInteger(IRDataTypes.Int32, (long)this.Module.ConstantsSize);
            return heapPointer;
        }

        IRFunction EmitPanic()
        {
            IRFunction panic = this.FunctionFactory.CreatePanic();
            IRLocal messageParameter = panic.Parameters.First();

            IRFunction errorFunction = this.Module.GetOrCreateFunctionImport("console", "error", new IRType(IRDataTypes.Void), [new IRLocal("message", new IRType(IRDataTypes.String), true)]);

            IRBuilder builder = new IRBuilder(Module, panic, panic.Entryblock);
            IRValueInstruction errorString = builder.BuildLoad(messageParameter);
            builder.BuildCall(errorFunction, [errorString]);
            builder.BuildBytes([0x00]);

            return panic;
        }

        IRFunction EmitMalloc(IRGlobal heapPointer) // TODO: Impliment an actual malloc, with blocks and all of that.
        {
            IRFunction malloc = this.FunctionFactory.CreateMalloc();
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

        IRFunction EmitFree(IRGlobal heapPointer)
        {
            IRFunction free = this.FunctionFactory.CreateFree();



            return free;
        }
    }
}

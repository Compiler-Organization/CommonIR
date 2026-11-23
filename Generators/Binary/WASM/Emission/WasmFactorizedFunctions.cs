using CommonIR.Generators.Binary.WASM.Model;
using CommonIR.Generators.Binary.WASM.Model.Sections;
using CommonIR.IR;
using CommonIR.IR.Factories;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.ControlFlow;
using CommonIR.IR.Grammar.Instructions.Memory;
using CommonIR.IR.Grammar.Instructions.Numeric;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CommonIR.Generators.Binary.WASM.Emission
{
    internal class WasmFactorizedFunctions
    {
        /// <summary>
        /// Constant data starts here, so that no string or allocation ever has the address 0, which is null.
        /// </summary>
        public const int DataStart = 16;

        public const string DivideByZeroMessage = "Attempted to divide by zero.";
        public const string IndexOutOfRangeMessage = "Index was outside the bounds of the array.";
        public const string NullReferenceMessage = "Object reference not set to an instance of an object.";
        public const string NegativeSizeMessage = "Arrays cannot have a negative size.";
        public const string OutOfMemoryMessage = "Out of memory.";

        IRModule Module { get; set; }

        FunctionFactory FunctionFactory { get; set; }

        public IRGlobal HeapPointer { get; set; }
        public IRFunction Malloc { get; set; }
        public IRFunction Free { get; set; }
        public IRFunction Panic { get; set; }

        /// <summary>
        /// The index of a function in the module's function table, which is what a function reference evaluates to.
        /// Set by the section translator.
        /// </summary>
        public Func<IRFunction, int> TableIndexOf { get; set; } = _ => throw new InvalidOperationException("The function table has not been created.");

        /// <summary>
        /// The index of the type with these parameters and results, added to the type section if it does not exist yet.
        /// Set by the section translator.
        /// </summary>
        public Func<List<WasmFormTypes>, List<WasmFormTypes>, ulong> TypeIndexOf { get; set; } = (_, _) => throw new InvalidOperationException("The type section has not been created.");

        public IRFunction DivideByZeroPanic { get; set; }
        public IRFunction IndexOutOfRangePanic { get; set; }
        public IRFunction NullReferencePanic { get; set; }
        public IRFunction NegativeSizePanic { get; set; }
        public IRFunction OutOfMemoryPanic { get; set; }

        public WasmFactorizedFunctions(IRModule module)
        {
            this.Module = module;
            this.FunctionFactory = new FunctionFactory(module);

            this.Panic = EmitPanic();
            this.DivideByZeroPanic = EmitPanicWith("__panic_divide_by_zero", DivideByZeroMessage);
            this.IndexOutOfRangePanic = EmitPanicWith("__panic_index_out_of_range", IndexOutOfRangeMessage);
            this.NullReferencePanic = EmitPanicWith("__panic_null_reference", NullReferenceMessage);
            this.NegativeSizePanic = EmitPanicWith("__panic_negative_size", NegativeSizeMessage);
            this.OutOfMemoryPanic = EmitPanicWith("__panic_out_of_memory", OutOfMemoryMessage);

            // The heap starts after the constant data, so every string constant has to exist by now.
            this.HeapPointer = EmitHeapPointer();
            this.Malloc = EmitMalloc();
            this.Free = EmitFree();
        }

        /// <summary>
        /// The address of the allocator's free lists: one 4-byte head per size class, placed right after the constant data.
        /// </summary>
        int FreeListsAddress => (int)((DataStart + (long)this.Module.ConstantsSize + 7) & ~7L);

        /// <summary>
        /// Blocks are 8, 16, ... 64 bytes (classes 0 to 7), then four sizes per power of two, so a block wastes at most
        /// a quarter of its size. Requests up to 2^31 bytes need 108 classes.
        /// </summary>
        const int SizeClassCount = 108;

        const int FreeListsSize = (SizeClassCount * 4 + 7) & ~7;

        /// <summary>
        /// Where the heap starts. Everything below it is constant data or allocator state.
        /// </summary>
        public int HeapStart => this.FreeListsAddress + FreeListsSize;

        IRGlobal EmitHeapPointer()
        {
            IRGlobal heapPointer = this.FunctionFactory.CreateHeapPointer();
            heapPointer.InitialValue = new IRConstantInteger(IRDataTypes.Int32, this.HeapStart);
            return heapPointer;
        }

        IRFunction EmitPanic()
        {
            IRFunction panic = this.FunctionFactory.CreatePanic();
            IRLocal messageParameter = panic.Parameters.First();

            IRFunction errorFunction = this.Module.GetOrCreateFunctionImport("console", "error", new IRType(IRDataTypes.Void), [new IRLocal("message", new IRType(IRDataTypes.String), true)]);

            IRBuilder builder = new IRBuilder(Module, panic, panic.Entryblock);
            IRValueInstruction errorString = builder.BuildLoad(messageParameter, messageParameter.ValueType);
            builder.BuildCall(errorFunction, [errorString]);
            builder.BuildBytes([0x00]);

            return panic;
        }

        /// <summary>
        /// A parameterless function that panics with a fixed message, called by the checks the emitter inserts.
        /// </summary>
        IRFunction EmitPanicWith(string name, string message)
        {
            IRFunction function = this.Module.CreateFunction(name, [new IRType(IRDataTypes.Void)], [], isExport: false);
            IRBuilder builder = new IRBuilder(Module, function, function.Entryblock);
            builder.BuildPanic(builder.BuildConstantString(message));
            return function;
        }

        /// <summary>
        /// A size-class allocator. Every block has an 8-byte header holding its class. Freed blocks go on the free list of
        /// their class (linked through their first 4 bytes) and are reused, zeroed, by later requests of that class.
        /// New blocks come from the end of the heap, which grows memory when it reaches its end.
        /// Its body is written directly in WebAssembly by <see cref="EmitMallocBody"/>.
        /// </summary>
        IRFunction EmitMalloc()
        {
            IRFunction malloc = this.FunctionFactory.CreateMalloc();

            malloc.CreateLocal("size", new IRType(IRDataTypes.Int32), isMutable: true);
            malloc.CreateLocal("class", new IRType(IRDataTypes.Int32), isMutable: true);
            malloc.CreateLocal("block", new IRType(IRDataTypes.Int32), isMutable: true);
            malloc.CreateLocal("next", new IRType(IRDataTypes.Int32), isMutable: true);

            return malloc;
        }

        static byte[] LocalGet(IRLocal local) => [(byte)WasmOpCodes.Local_get, .. LEB128.EncodeUnsigned(local.Offset)];
        static byte[] LocalSet(IRLocal local) => [(byte)WasmOpCodes.Local_set, .. LEB128.EncodeUnsigned(local.Offset)];
        static byte[] LocalTee(IRLocal local) => [(byte)WasmOpCodes.Local_tee, .. LEB128.EncodeUnsigned(local.Offset)];
        static byte[] I32Const(int value) => [(byte)WasmOpCodes.I32_const, .. LEB128.EncodeSigned(value)];
        static byte[] I32Load(int offset) => [(byte)WasmOpCodes.I32_load, 0x02, .. LEB128.EncodeUnsigned((ulong)offset)];
        static byte[] I32Store(int offset) => [(byte)WasmOpCodes.I32_store, 0x02, .. LEB128.EncodeUnsigned((ulong)offset)];
        static byte[] CallFunction(IRFunction function) => [(byte)WasmOpCodes.Call, .. LEB128.EncodeUnsigned(function.Offset)];

        /// <summary>
        /// Adds 1 or -1 to the live allocation counter of the module, if it has one.
        /// </summary>
        byte[] CountAllocation(int change)
        {
            IRGlobal? counter = this.Module.Globals.FirstOrDefault(g => g.Name == IRFree.LiveAllocationsGlobal);

            if (counter == null)
            {
                return [];
            }

            return [
                (byte)WasmOpCodes.Global_get, .. LEB128.EncodeUnsigned(counter.Offset),
                (byte)WasmOpCodes.I64_const, .. LEB128.EncodeSigned(change),
                (byte)WasmOpCodes.I64_add,
                (byte)WasmOpCodes.Global_set, .. LEB128.EncodeUnsigned(counter.Offset),
            ];
        }

        /// <summary>
        /// The body of <see cref="Malloc"/>. Needs the final local, global and function indices.
        /// </summary>
        public List<byte> EmitMallocBody()
        {
            IRLocal bytes = this.Malloc.Parameters[0];
            IRLocal size = this.Malloc.Locals[0];
            IRLocal sizeClass = this.Malloc.Locals[1];
            IRLocal block = this.Malloc.Locals[2];
            IRLocal next = this.Malloc.Locals[3];

            byte[] MemoryEnd() => [(byte)WasmOpCodes.Memory_size, 0x00, .. I32Const(16), (byte)WasmOpCodes.I32_shl];
            byte[] OutOfMemory() => CallFunction(this.OutOfMemoryPanic);

            return [
                // if (bytes < 8) bytes = 8
                .. LocalGet(bytes),
                .. I32Const(8),
                (byte)WasmOpCodes.I32_lt_u,
                (byte)WasmOpCodes.If, (byte)WasmFormTypes.Void,
                    .. I32Const(8),
                    .. LocalSet(bytes),
                (byte)WasmOpCodes.End,

                .. LocalGet(bytes),
                .. I32Const(64),
                (byte)WasmOpCodes.I32_le_u,
                (byte)WasmOpCodes.If, (byte)WasmFormTypes.Void,
                    // size = (bytes + 7) & ~7, class = size / 8 - 1
                    .. LocalGet(bytes),
                    .. I32Const(7),
                    (byte)WasmOpCodes.I32_add,
                    .. I32Const(~7),
                    (byte)WasmOpCodes.I32_and,
                    .. LocalTee(size),
                    .. I32Const(3),
                    (byte)WasmOpCodes.I32_shr_u,
                    .. I32Const(1),
                    (byte)WasmOpCodes.I32_sub,
                    .. LocalSet(sizeClass),
                (byte)WasmOpCodes.Else,
                    .. LocalGet(bytes),
                    .. I32Const(int.MaxValue - 7),
                    (byte)WasmOpCodes.I32_gt_u,
                    (byte)WasmOpCodes.If, (byte)WasmFormTypes.Void,
                        .. OutOfMemory(),
                    (byte)WasmOpCodes.End,

                    // next = k, the power of two with 2^k < bytes <= 2^(k+1)
                    .. I32Const(31),
                    .. LocalGet(bytes),
                    .. I32Const(1),
                    (byte)WasmOpCodes.I32_sub,
                    0x67, // i32.clz
                    (byte)WasmOpCodes.I32_sub,
                    .. LocalSet(next),

                    // block = step - 1, with step = 2^(k-2)
                    .. I32Const(1),
                    .. LocalGet(next),
                    .. I32Const(2),
                    (byte)WasmOpCodes.I32_sub,
                    (byte)WasmOpCodes.I32_shl,
                    .. I32Const(1),
                    (byte)WasmOpCodes.I32_sub,
                    .. LocalSet(block),

                    // size = bytes rounded up to a multiple of step
                    .. LocalGet(bytes),
                    .. LocalGet(block),
                    (byte)WasmOpCodes.I32_add,
                    .. LocalGet(block),
                    .. I32Const(-1),
                    (byte)WasmOpCodes.I32_xor,
                    (byte)WasmOpCodes.I32_and,
                    .. LocalSet(size),

                    // class = 8 + (k - 6) * 4 + (size / step - 5)
                    .. LocalGet(next),
                    .. I32Const(6),
                    (byte)WasmOpCodes.I32_sub,
                    .. I32Const(2),
                    (byte)WasmOpCodes.I32_shl,
                    .. LocalGet(size),
                    .. LocalGet(next),
                    .. I32Const(2),
                    (byte)WasmOpCodes.I32_sub,
                    (byte)WasmOpCodes.I32_shr_u,
                    (byte)WasmOpCodes.I32_add,
                    .. I32Const(3),
                    (byte)WasmOpCodes.I32_add,
                    .. LocalSet(sizeClass),
                (byte)WasmOpCodes.End,

                .. CountAllocation(1),

                // Reuse the first free block of the class, if there is one.
                .. LocalGet(sizeClass),
                .. I32Const(2),
                (byte)WasmOpCodes.I32_shl,
                .. I32Load(this.FreeListsAddress),
                .. LocalTee(block),
                (byte)WasmOpCodes.If, (byte)WasmFormTypes.Void,
                    .. LocalGet(sizeClass),
                    .. I32Const(2),
                    (byte)WasmOpCodes.I32_shl,
                    .. LocalGet(block),
                    .. I32Load(0),
                    .. I32Store(this.FreeListsAddress),

                    // memory.fill(block, 0, size)
                    .. LocalGet(block),
                    .. I32Const(0),
                    .. LocalGet(size),
                    0xFC, 0x0B, 0x00,

                    .. LocalGet(block),
                    (byte)WasmOpCodes.Return,
                (byte)WasmOpCodes.End,

                // block = heap + 8 (after its header), next = block + size
                (byte)WasmOpCodes.Global_get, .. LEB128.EncodeUnsigned(this.HeapPointer.Offset),
                .. I32Const(8),
                (byte)WasmOpCodes.I32_add,
                .. LocalTee(block),
                .. LocalGet(size),
                (byte)WasmOpCodes.I32_add,
                .. LocalTee(next),

                // if (next < block) the address space is exhausted
                .. LocalGet(block),
                (byte)WasmOpCodes.I32_lt_u,
                (byte)WasmOpCodes.If, (byte)WasmFormTypes.Void,
                    .. OutOfMemory(),
                (byte)WasmOpCodes.End,

                // if (next > memory end) grow by the missing number of 64 KiB pages, or panic
                .. LocalGet(next),
                .. MemoryEnd(),
                (byte)WasmOpCodes.I32_gt_u,
                (byte)WasmOpCodes.If, (byte)WasmFormTypes.Void,
                    .. LocalGet(next),
                    .. MemoryEnd(),
                    (byte)WasmOpCodes.I32_sub,
                    .. I32Const(65535),
                    (byte)WasmOpCodes.I32_add,
                    .. I32Const(16),
                    (byte)WasmOpCodes.I32_shr_u,
                    (byte)WasmOpCodes.Memory_grow, 0x00,
                    .. I32Const(-1),
                    (byte)WasmOpCodes.I32_eq,
                    (byte)WasmOpCodes.If, (byte)WasmFormTypes.Void,
                        .. OutOfMemory(),
                    (byte)WasmOpCodes.End,
                (byte)WasmOpCodes.End,

                // header = class
                .. LocalGet(block),
                .. I32Const(8),
                (byte)WasmOpCodes.I32_sub,
                .. LocalGet(sizeClass),
                .. I32Store(0),

                .. LocalGet(next),
                (byte)WasmOpCodes.Global_set, .. LEB128.EncodeUnsigned(this.HeapPointer.Offset),
                .. LocalGet(block),
            ];
        }

        /// <summary>
        /// Puts a block back on the free list of its class. Null and addresses below the heap (constant data) are ignored.
        /// Its body is written directly in WebAssembly by <see cref="EmitFreeBody"/>.
        /// </summary>
        IRFunction EmitFree()
        {
            IRFunction free = this.FunctionFactory.CreateFree();
            free.CreateLocal("class", new IRType(IRDataTypes.Int32), isMutable: true);
            return free;
        }

        /// <summary>
        /// The body of <see cref="Free"/>. Needs the final local, global and function indices.
        /// </summary>
        public List<byte> EmitFreeBody()
        {
            IRLocal pointer = this.Free.Parameters[0];
            IRLocal sizeClass = this.Free.Locals[0];

            return [
                .. LocalGet(pointer),
                .. I32Const(this.HeapStart + 8),
                (byte)WasmOpCodes.I32_lt_u,
                (byte)WasmOpCodes.If, (byte)WasmFormTypes.Void,
                    (byte)WasmOpCodes.Return,
                (byte)WasmOpCodes.End,

                .. CountAllocation(-1),

                // class offset = header * 4
                .. LocalGet(pointer),
                .. I32Const(8),
                (byte)WasmOpCodes.I32_sub,
                .. I32Load(0),
                .. I32Const(2),
                (byte)WasmOpCodes.I32_shl,
                .. LocalSet(sizeClass),

                // *pointer = lists[class], lists[class] = pointer
                .. LocalGet(pointer),
                .. LocalGet(sizeClass),
                .. I32Load(this.FreeListsAddress),
                .. I32Store(0),
                .. LocalGet(sizeClass),
                .. LocalGet(pointer),
                .. I32Store(this.FreeListsAddress),
            ];
        }
    }
}

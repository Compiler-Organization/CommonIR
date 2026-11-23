using CommonIR.Errors;
using CommonIR.Generators.Binary.WASM.Model;
using CommonIR.Generators.Binary.WASM.Translation;
using CommonIR.Generators.Binary.WASM.Model.Sections;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.Arithmetic;
using CommonIR.IR.Grammar.Instructions.ControlFlow;
using CommonIR.IR.Grammar.Instructions.Memory;
using CommonIR.IR.Grammar.Instructions.Numeric;
using CommonIR.IR.Grammar.Objects;
using CommonIR.Passes.Optimization;
using System.Text;

namespace CommonIR.Generators.Binary.WASM.Emission
{
    /// <summary>
    /// Emits WebAssembly bytecode for IR instructions.
    ///
    /// Values narrower than 32 bits live in i32s and are kept normalized the same way the x86-64 target keeps them:
    /// Int8/Int16 sign-extended, UInt8/UInt16/Bool zero-extended. Pointers, structs and the halves of strings and arrays
    /// are i32 addresses; integer operands of a different width (such as 64-bit sizes and offsets) are converted where they meet.
    /// </summary>
    internal class WasmInstructionEmitter
    {
        Stack<IRBlock> BlockContextStack { get; set; } = new Stack<IRBlock>();

        IRFunction? Function { get; set; }

        WasmScratchPool? ScratchPool { get; set; }

        WasmFactorizedFunctions FactorizedFunctions { get; set; }

        public WasmInstructionEmitter(IRFunction? function, WasmFactorizedFunctions factorizedFunctions)
        {
            this.Function = function;
            this.FactorizedFunctions = factorizedFunctions;

            if(function != null)
            {
                this.ScratchPool = new WasmScratchPool(function);
                BlockContextStack.Push(function.Entryblock);
            }
        }

        WasmOpCodes AddComplexity(WasmOpCodes opCode)
        {
            BlockContextStack.Peek().SpeculativeCircuitComplexity += (ulong)WasmOpCodeComplexity.Of(opCode);
            return opCode;
        }

        public List<byte> EmitInstructions(List<IRInstruction> instructions)
            => instructions.SelectMany(EmitStatement).ToList();

        /// <summary>
        /// Emits an instruction placed directly in a block. Calls are placed in blocks even when they return a value,
        /// so that they run at that point; their result is kept in a local for the instructions that use it later.
        /// </summary>
        byte[] EmitStatement(IRInstruction instruction)
        {
            if (instruction is not IRValueInstruction value
                || value is IRLocal or IRGlobal or IRStructProperty
                || value.IsConstant
                || value.ValueType.DataType == IRDataTypes.Void
                || CachedInstructions.ContainsKey(value))
            {
                return EmitInstruction(instruction);
            }

            if (this.Function == null)
            {
                throw ErrorHandler.Create($"Cannot keep the result of '{value.Dump(0)}': Instruction emitter did not receive a target function.");
            }

            List<byte> bytecode = [.. EmitUncached(value)];

            IRLocal result = WasmLocalLayout.CreateLocal(this.Function, $"$cache_{CachedInstructions.Count}", value.ValueType);
            CachedInstructions.Add(value, result);

            if (value.ValueType.IsFatPointer)
            {
                bytecode.AddRange(LocalSet(result.LengthCompanion!));
            }

            bytecode.AddRange(LocalSet(result));
            return bytecode.ToArray();
        }

        public List<byte> EmitValueInstructions(List<IRValueInstruction> valueInstructions)
            => valueInstructions.SelectMany(EmitInstruction).ToList();

        /// <summary>
        /// Cache to replace a instruction used multipe times with a local set / load.
        /// </summary>
        Dictionary<IRValueInstruction, IRLocal> CachedInstructions = new Dictionary<IRValueInstruction, IRLocal>();

        public byte[] EmitInstruction(IRInstruction instruction)
        {
            if (instruction is IRValueInstruction valueInstruction && CachedInstructions.TryGetValue(valueInstruction, out IRLocal? localCache))
            {
                return EmitLoadLocal(localCache);
            }

            List<byte> bytecode = [.. EmitUncached(instruction)];

            if (instruction is IRValueInstruction valInstruction
                && valInstruction is not (IRLocal or IRGlobal or IRStructProperty)
                && !valInstruction.IsConstant
                && valInstruction.ValueType.DataType != IRDataTypes.Void
                && valInstruction.References.Count > 1)
            {
                if (this.Function == null)
                {
                    throw ErrorHandler.Create($"Cannot create temporary cache for '{valInstruction.Dump(0)}': Instruction emitter did not receive a target function.");
                }

                IRLocal newLocalCache = WasmLocalLayout.CreateLocal(this.Function, $"$cache_{CachedInstructions.Count}", valInstruction.ValueType);
                CachedInstructions.Add(valInstruction, newLocalCache);

                if (valInstruction.ValueType.IsFatPointer)
                {
                    bytecode.AddRange(LocalSet(newLocalCache.LengthCompanion!));
                    bytecode.AddRange(LocalSet(newLocalCache));
                    bytecode.AddRange(EmitLoadLocal(newLocalCache));
                }
                else
                {
                    bytecode.AddRange(LocalTee(newLocalCache));
                }
            }

            return bytecode.ToArray();
        }

        byte[] EmitUncached(IRInstruction instruction)
        {
            return instruction switch
            {
                IRAdd add => EmitArithmetic("IRAdd", add.Left, add.Right, add.ValueType, WasmOpCodes.I32_add, WasmOpCodes.I64_add, WasmOpCodes.F32_add, WasmOpCodes.F64_add),
                IRSubtract subtract => EmitArithmetic("IRSubtract", subtract.Left, subtract.Right, subtract.ValueType, WasmOpCodes.I32_sub, WasmOpCodes.I64_sub, WasmOpCodes.F32_sub, WasmOpCodes.F64_sub),
                IRMultiply multiply => EmitArithmetic("IRMultiply", multiply.Left, multiply.Right, multiply.ValueType, WasmOpCodes.I32_mul, WasmOpCodes.I64_mul, WasmOpCodes.F32_mul, WasmOpCodes.F64_mul),
                IRDivide divide => EmitDivide(divide),
                IRBitwise bitwise => EmitBitwise(bitwise),
                IRConvert convert => EmitConvert(convert),
                IRMath math => EmitMath(math),

                IRConstantInteger i => EmitConstant(i),
                IRCall call => EmitCall(call),
                IRBlock block => EmitBlock(block),
                IRReturn ret => EmitReturn(ret),
                IRLoad load => EmitLoad(load),
                IRStore store => EmitStore(store),
                IRLoadElement loadElement => EmitLoadElement(loadElement),
                IRStoreElement storeElement => EmitStoreElement(storeElement),
                IRLength length => EmitLength(length),
                IRFatPointer fatPointer => EmitFatPointer(fatPointer),
                IRCompare compare => EmitCompare(compare),
                IRConditionalBranch conditionalBranch => EmitConditionalBranch(conditionalBranch),
                IRLoop loop => EmitLoop(loop),
                IRBreak => EmitBreak(),
                IRContinue => EmitContinue(),

                IRConstantString str => EmitLoadString(str),
                IRGlobal global => EmitLoadGlobal(global),
                IRLocal local => EmitLoadLocal(local),
                IRBytes bytes => EmitBytes(bytes),

                IRMalloc malloc => EmitMalloc(malloc),
                IRFree free => [.. EmitAs(free.Pointer, WasmFormTypes.I32), .. Call(this.FactorizedFunctions.Free)],
                IRPanic panic => EmitPanic(panic),

                IRFunctionReference functionReference => I32Const(this.FactorizedFunctions.TableIndexOf(functionReference.Function)),
                IRCallIndirect callIndirect => EmitCallIndirect(callIndirect),

                IRDispatch dispatch => EmitDispatch(dispatch),
                IRAwait await => EmitAwait(await),

                IRInstantiateArray instantiateArray => EmitInstantiateArray(instantiateArray),
                IRInstantiateStruct instantiateStruct => EmitInstantiateStruct(instantiateStruct),
                IRStructProperty property => EmitProperty(property),

                _ => throw ErrorHandler.CreateNotImplimented($"No Wasm translation implemented for instruction '{instruction.GetType().Name}'")
            };
        }

        #region Helpers

        static WasmFormTypes WasmType(IRType type) => WasmTypeTranslator.TranslateIRType(type);

        static bool IsSigned(IRDataTypes dataType) => dataType is IRDataTypes.Int8 or IRDataTypes.Int16 or IRDataTypes.Int32 or IRDataTypes.Int64;

        static bool IsFloat(WasmFormTypes type) => type is WasmFormTypes.F32 or WasmFormTypes.F64;

        byte[] Op(WasmOpCodes opCode) => [(byte)AddComplexity(opCode)];

        byte[] LocalGet(IRLocal local) => [(byte)AddComplexity(WasmOpCodes.Local_get), .. LEB128.EncodeUnsigned(local.Offset)];

        byte[] LocalSet(IRLocal local) => [(byte)AddComplexity(WasmOpCodes.Local_set), .. LEB128.EncodeUnsigned(local.Offset)];

        byte[] LocalTee(IRLocal local) => [(byte)AddComplexity(WasmOpCodes.Local_tee), .. LEB128.EncodeUnsigned(local.Offset)];

        byte[] I32Const(long value) => [(byte)AddComplexity(WasmOpCodes.I32_const), .. LEB128.EncodeSigned(value)];

        byte[] I64Const(long value) => [(byte)AddComplexity(WasmOpCodes.I64_const), .. LEB128.EncodeSigned(value)];

        static int ElementWidth(IRType elementType) => IRStructSchema.IsInline(elementType) ? ((IRStructSchema)elementType.UserObject!).Width : elementType.Width;

        static int MaxArrayLength(IRType elementType) => (int.MaxValue - 7) / Math.Max(ElementWidth(elementType), 1);

        byte[] Call(IRFunction function) => [(byte)AddComplexity(WasmOpCodes.Call), .. LEB128.EncodeUnsigned(function.Offset)];

        /// <summary>
        /// A load or store with alignment hint 0: struct properties are packed, so addresses are not necessarily aligned.
        /// </summary>
        byte[] MemoryAccess(WasmOpCodes opCode, ulong offset = 0) => [(byte)AddComplexity(opCode), .. LEB128.EncodeUnsigned(0), .. LEB128.EncodeUnsigned(offset)];

        /// <summary>
        /// Calls <paramref name="panic"/> when the i32 on top of the stack is non-zero. Consumes the i32.
        /// </summary>
        byte[] PanicIf(IRFunction panic) => [(byte)AddComplexity(WasmOpCodes.If), (byte)WasmFormTypes.Void, .. Call(panic), (byte)AddComplexity(WasmOpCodes.End)];

        IRLocal Borrow(IRDataTypes dataType) => this.ScratchPool?.Borrow(dataType) ?? throw ErrorHandler.Create("Instruction emitter did not receive a target function.");

        /// <summary>
        /// Emits a value and converts it to the given WebAssembly type. A string or array converts to its pointer.
        /// </summary>
        byte[] EmitAs(IRValueInstruction value, WasmFormTypes target)
        {
            byte[] bytes = EmitInstruction(value);

            if (value.ValueType.IsFatPointer)
            {
                return [.. bytes, .. Op(WasmOpCodes.Drop), .. Coerce(new IRType(IRDataTypes.Pointer), target)];
            }

            return [.. bytes, .. Coerce(value.ValueType, target)];
        }

        byte[] Coerce(IRType from, WasmFormTypes target)
        {
            WasmFormTypes source = WasmType(from);

            return (source, target) switch
            {
                _ when source == target => [],
                (WasmFormTypes.I64, WasmFormTypes.I32) => Op(WasmOpCodes.I32_wrap_i64),
                (WasmFormTypes.I32, WasmFormTypes.I64) => Op(IsSigned(from.DataType) ? WasmOpCodes.I64_extend_i32_s : WasmOpCodes.I64_extend_i32_u),
                _ => throw ErrorHandler.Create($"Cannot use a value of type '{from.Dump(0)}' where a {target} is expected.")
            };
        }

        /// <summary>
        /// Truncates an i32 result to the width of its IR type, like the x86-64 target does after every operation.
        /// </summary>
        byte[] Normalize(IRDataTypes dataType) => dataType switch
        {
            IRDataTypes.Int8 => Op(WasmOpCodes.I32_extend8_s),
            IRDataTypes.Int16 => Op(WasmOpCodes.I32_extend16_s),
            IRDataTypes.UInt8 or IRDataTypes.Bool => [.. I32Const(0xFF), .. Op(WasmOpCodes.I32_and)],
            IRDataTypes.UInt16 => [.. I32Const(0xFFFF), .. Op(WasmOpCodes.I32_and)],
            _ => []
        };

        static WasmOpCodes LoadOpCode(IRType type) => type.DataType switch
        {
            IRDataTypes.Int8 => WasmOpCodes.I32_load8_s,
            IRDataTypes.UInt8 or IRDataTypes.Bool => WasmOpCodes.I32_load8_u,
            IRDataTypes.Int16 => WasmOpCodes.I32_load16_s,
            IRDataTypes.UInt16 => WasmOpCodes.I32_load16_u,
            IRDataTypes.Int64 or IRDataTypes.UInt64 => WasmOpCodes.I64_load,
            IRDataTypes.Float32 => WasmOpCodes.F32_load,
            IRDataTypes.Float64 => WasmOpCodes.F64_load,
            _ => WasmOpCodes.I32_load
        };

        static WasmOpCodes StoreOpCode(IRType type) => type.DataType switch
        {
            IRDataTypes.Int8 or IRDataTypes.UInt8 or IRDataTypes.Bool => WasmOpCodes.I32_store8,
            IRDataTypes.Int16 or IRDataTypes.UInt16 => WasmOpCodes.I32_store16,
            IRDataTypes.Int64 or IRDataTypes.UInt64 => WasmOpCodes.I64_store,
            IRDataTypes.Float32 => WasmOpCodes.F32_store,
            IRDataTypes.Float64 => WasmOpCodes.F64_store,
            _ => WasmOpCodes.I32_store
        };

        /// <summary>
        /// Loads a value of the given type from the address on top of the stack.
        /// </summary>
        byte[] LoadFromAddress(IRType type)
        {
            if (!type.IsFatPointer)
            {
                return MemoryAccess(LoadOpCode(type));
            }

            IRLocal address = Borrow(IRDataTypes.Int32);

            byte[] bytes = [
                .. LocalTee(address),
                .. MemoryAccess(WasmOpCodes.I32_load),
                .. LocalGet(address),
                .. MemoryAccess(WasmOpCodes.I32_load, 4),
            ];

            this.ScratchPool!.Return(address);
            return bytes;
        }

        /// <summary>
        /// Stores <paramref name="value"/> at the address on top of the stack.
        /// </summary>
        byte[] StoreToAddress(IRValueInstruction value, IRType type, ulong offset = 0)
        {
            if (!type.IsFatPointer)
            {
                return [.. EmitAs(value, WasmType(type)), .. MemoryAccess(StoreOpCode(type), offset)];
            }

            IRLocal address = Borrow(IRDataTypes.Int32);
            IRLocal length = Borrow(IRDataTypes.Int32);

            byte[] bytes = [
                .. LocalTee(address),
                .. EmitInstruction(value),
                .. LocalSet(length),
                .. MemoryAccess(WasmOpCodes.I32_store, offset),
                .. LocalGet(address),
                .. LocalGet(length),
                .. MemoryAccess(WasmOpCodes.I32_store, offset + 4),
            ];

            this.ScratchPool!.Return(address);
            this.ScratchPool!.Return(length);
            return bytes;
        }

        #endregion

        public byte[] EmitPanic(IRPanic panic)
        {
            return [.. EmitInstruction(panic.Message), .. Call(this.FactorizedFunctions.Panic)];
        }

        /// <summary>
        /// WebAssembly has no GPU, so a kernel runs on the CPU: once for every index, before the dispatch returns.
        /// The handle is the address of the finished output array (its pointer and length).
        /// </summary>
        public byte[] EmitDispatch(IRDispatch dispatch)
        {
            IRFunction kernel = dispatch.Kernel;

            if (!kernel.HasKernelOutput)
            {
                return EmitInPlaceDispatch(dispatch);
            }

            IRType outputType = kernel.KernelOutput.ValueType;
            IRType elementType = (outputType.UserObject as IRArraySchema)?.ElementType
                ?? throw ErrorHandler.Create($"The output of kernel '{kernel.Name}' is not an array.");

            IRLocal count = Borrow(IRDataTypes.Int32);
            IRLocal output = Borrow(IRDataTypes.Int32);
            IRLocal index = Borrow(IRDataTypes.Int32);
            IRLocal handle = Borrow(IRDataTypes.Int32);

            List<byte> bytecode = [
                .. EmitAs(dispatch.Count, WasmFormTypes.I32),
                .. LocalTee(count),
                .. I32Const(elementType.Width),
                .. Op(WasmOpCodes.I32_mul),
                .. Call(this.FactorizedFunctions.Malloc),
                .. LocalSet(output),

                .. I32Const(0),
                .. LocalSet(index),

                (byte)AddComplexity(WasmOpCodes.Block), (byte)WasmFormTypes.Void,
                (byte)AddComplexity(WasmOpCodes.Loop), (byte)WasmFormTypes.Void,
                .. LocalGet(index),
                .. LocalGet(count),
                .. Op(WasmOpCodes.I32_ge_u),
                (byte)AddComplexity(WasmOpCodes.Br_if), .. LEB128.EncodeUnsigned(1),

                .. LocalGet(index),
                .. LocalGet(output),
                .. LocalGet(count),
            ];

            List<IRLocal> parameters = kernel.KernelParameters;

            for (int i = 0; i < dispatch.Arguments.Count; i++)
            {
                IRValueInstruction argument = dispatch.Arguments[i];
                IRType? parameterType = i < parameters.Count ? parameters[i].ValueType : null;

                bytecode.AddRange(parameterType == null || parameterType.IsFatPointer || argument.ValueType.IsFatPointer
                    ? EmitInstruction(argument)
                    : EmitAs(argument, WasmType(parameterType)));
            }

            bytecode.AddRange([
                .. Call(kernel),

                .. LocalGet(index),
                .. I32Const(1),
                .. Op(WasmOpCodes.I32_add),
                .. LocalSet(index),
                (byte)AddComplexity(WasmOpCodes.Br), .. LEB128.EncodeUnsigned(0),
                (byte)AddComplexity(WasmOpCodes.End),
                (byte)AddComplexity(WasmOpCodes.End),

                .. I32Const(8),
                .. Call(this.FactorizedFunctions.Malloc),
                .. LocalTee(handle),
                .. LocalGet(output),
                .. MemoryAccess(WasmOpCodes.I32_store),
                .. LocalGet(handle),
                .. LocalGet(count),
                .. MemoryAccess(WasmOpCodes.I32_store, 4),
                .. LocalGet(handle),
            ]);

            this.ScratchPool!.Return(handle);
            this.ScratchPool!.Return(index);
            this.ScratchPool!.Return(output);
            this.ScratchPool!.Return(count);
            return bytecode.ToArray();
        }

        byte[] EmitInPlaceDispatch(IRDispatch dispatch)
        {
            IRFunction kernel = dispatch.Kernel;
            IRLocal count = Borrow(IRDataTypes.Int32);
            IRLocal index = Borrow(IRDataTypes.Int32);

            List<byte> bytecode = [
                .. EmitAs(dispatch.Count, WasmFormTypes.I32),
                .. LocalSet(count),
                .. I32Const(0),
                .. LocalSet(index),

                (byte)AddComplexity(WasmOpCodes.Block), (byte)WasmFormTypes.Void,
                (byte)AddComplexity(WasmOpCodes.Loop), (byte)WasmFormTypes.Void,
                .. LocalGet(index),
                .. LocalGet(count),
                .. Op(WasmOpCodes.I32_ge_u),
                (byte)AddComplexity(WasmOpCodes.Br_if), .. LEB128.EncodeUnsigned(1),

                .. LocalGet(index),
            ];

            List<IRLocal> parameters = kernel.KernelParameters;

            for (int i = 0; i < dispatch.Arguments.Count; i++)
            {
                IRValueInstruction argument = dispatch.Arguments[i];
                IRType? parameterType = i < parameters.Count ? parameters[i].ValueType : null;

                bytecode.AddRange(parameterType == null || parameterType.IsFatPointer || argument.ValueType.IsFatPointer
                    ? EmitInstruction(argument)
                    : EmitAs(argument, WasmType(parameterType)));
            }

            bytecode.AddRange([
                .. Call(kernel),

                .. LocalGet(index),
                .. I32Const(1),
                .. Op(WasmOpCodes.I32_add),
                .. LocalSet(index),
                (byte)AddComplexity(WasmOpCodes.Br), .. LEB128.EncodeUnsigned(0),
                (byte)AddComplexity(WasmOpCodes.End),
                (byte)AddComplexity(WasmOpCodes.End),

                .. I32Const(0),
            ]);

            this.ScratchPool!.Return(index);
            this.ScratchPool!.Return(count);
            return bytecode.ToArray();
        }

        public byte[] EmitAwait(IRAwait await)
        {
            return [.. EmitAs(await.Handle, WasmFormTypes.I32), .. LoadFromAddress(await.ValueType)];
        }

        public byte[] EmitMalloc(IRMalloc malloc)
        {
            if (malloc.Bytes.ValueType.IsFatPointer || WasmType(malloc.Bytes.ValueType) != WasmFormTypes.I64)
            {
                return [.. EmitAs(malloc.Bytes, WasmFormTypes.I32), .. Call(this.FactorizedFunctions.Malloc)];
            }

            IRLocal bytes = Borrow(IRDataTypes.Int64);

            byte[] code = [
                .. EmitInstruction(malloc.Bytes),
                .. LocalTee(bytes),
                .. I64Const(int.MaxValue - 7),
                .. Op(WasmOpCodes.I64_gt_u),
                .. PanicIf(this.FactorizedFunctions.OutOfMemoryPanic),
                .. LocalGet(bytes),
                .. Op(WasmOpCodes.I32_wrap_i64),
                .. Call(this.FactorizedFunctions.Malloc),
            ];

            this.ScratchPool!.Return(bytes);
            return code;
        }

        public byte[] EmitBytes(IRBytes bytes)
        {
            return bytes.Bytes;
        }

        public byte[] EmitInstantiateStruct(IRInstantiateStruct instantiateStruct)
        {
            List<byte> bytecode = [];

            bytecode.AddRange(I32Const(instantiateStruct.StructSchema.Width));
            bytecode.AddRange(Call(this.FactorizedFunctions.Malloc));

            if (instantiateStruct.PropertyValues == null || instantiateStruct.PropertyValues.Count <= 0)
            {
                return bytecode.ToArray();
            }

            IRLocal structBasePointer = Borrow(IRDataTypes.Int32);
            bytecode.AddRange(LocalSet(structBasePointer));

            for (int i = 0; i < instantiateStruct.PropertyValues.Count; i++)
            {
                IRStructProperty property = instantiateStruct.StructSchema.Properties[i];
                IRValueInstruction propertyValue = instantiateStruct.PropertyValues[i];

                if(property.ValueType != propertyValue.ValueType)
                {
                    throw ErrorHandler.Create($"Struct property '{property.Name}' type mismatch. Expected '{property.ValueType.Dump(0)}', but got '{propertyValue.ValueType.Dump(0)}'");
                }

                bytecode.AddRange(LocalGet(structBasePointer));
                bytecode.AddRange(StoreToAddress(propertyValue, property.ValueType, (ulong)property.Offset));
            }

            bytecode.AddRange(LocalGet(structBasePointer));

            this.ScratchPool!.Return(structBasePointer);
            return bytecode.ToArray();
        }

        public byte[] EmitInstantiateArray(IRInstantiateArray instantiateArray)
        {
            List<byte> bytecode = [];

            IRType elementType = instantiateArray.ArraySchema.ElementType;
            IRLocal length = Borrow(IRDataTypes.Int32);
            IRLocal arrayPointer = Borrow(IRDataTypes.Int32);

            if(instantiateArray.HasFixedSize)
            {
                if(instantiateArray.Size is not IRConstantInteger integerSize)
                {
                    throw ErrorHandler.Create($"Array has a fixed size but the size instruction is not a constant integer: {instantiateArray.Size?.Dump(0)}");
                }

                if (integerSize.Value < 0 || integerSize.Value > MaxArrayLength(elementType))
                {
                    throw ErrorHandler.Create($"An array of {integerSize.Value} elements does not fit in WebAssembly memory.");
                }

                bytecode.AddRange(I32Const((long)integerSize.Value));
            }
            else
            {
                if (WasmType(instantiateArray.Size.ValueType) == WasmFormTypes.I64)
                {
                    IRLocal wideLength = Borrow(IRDataTypes.Int64);
                    bytecode.AddRange(EmitInstruction(instantiateArray.Size));
                    bytecode.AddRange(LocalTee(wideLength));
                    bytecode.AddRange(I64Const(0));
                    bytecode.AddRange(Op(IsSigned(instantiateArray.Size.ValueType.DataType) ? WasmOpCodes.I64_lt_s : WasmOpCodes.I64_lt_u));
                    bytecode.AddRange(PanicIf(this.FactorizedFunctions.NegativeSizePanic));
                    bytecode.AddRange(LocalGet(wideLength));
                    bytecode.AddRange(I64Const(MaxArrayLength(elementType)));
                    bytecode.AddRange(Op(WasmOpCodes.I64_gt_u));
                    bytecode.AddRange(PanicIf(this.FactorizedFunctions.OutOfMemoryPanic));
                    bytecode.AddRange(LocalGet(wideLength));
                    bytecode.AddRange(Op(WasmOpCodes.I32_wrap_i64));
                    bytecode.AddRange(LocalSet(length));
                    this.ScratchPool!.Return(wideLength);
                }
                else
                {
                    bytecode.AddRange(EmitAs(instantiateArray.Size, WasmFormTypes.I32));
                    bytecode.AddRange(LocalTee(length));
                    bytecode.AddRange(I32Const(0));
                    bytecode.AddRange(Op(IsSigned(instantiateArray.Size.ValueType.DataType) ? WasmOpCodes.I32_lt_s : WasmOpCodes.I32_lt_u));
                    bytecode.AddRange(PanicIf(this.FactorizedFunctions.NegativeSizePanic));
                }

                bytecode.AddRange(LocalGet(length));
                bytecode.AddRange(I32Const(MaxArrayLength(elementType)));
                bytecode.AddRange(Op(WasmOpCodes.I32_gt_u));
                bytecode.AddRange(PanicIf(this.FactorizedFunctions.OutOfMemoryPanic));
                bytecode.AddRange(LocalGet(length));
            }

            bytecode.AddRange(LocalTee(length));
            bytecode.AddRange(I32Const(ElementWidth(elementType)));
            bytecode.AddRange(Op(WasmOpCodes.I32_mul));
            bytecode.AddRange(Call(this.FactorizedFunctions.Malloc));
            bytecode.AddRange(LocalSet(arrayPointer));

            for(int i = 0; i < instantiateArray.ElementValues.Count; i++)
            {
                bytecode.AddRange(LocalGet(arrayPointer));
                bytecode.AddRange(IRStructSchema.IsInline(elementType)
                    ? [.. I32Const(i * ElementWidth(elementType)), .. Op(WasmOpCodes.I32_add), .. CopyStructToAddress(instantiateArray.ElementValues[i], (IRStructSchema)elementType.UserObject!)]
                    : StoreToAddress(instantiateArray.ElementValues[i], elementType, (ulong)(i * elementType.Width)));
            }

            bytecode.AddRange(LocalGet(arrayPointer));
            bytecode.AddRange(LocalGet(length));

            this.ScratchPool!.Return(arrayPointer);
            this.ScratchPool!.Return(length);
            return bytecode.ToArray();
        }

        public byte[] EmitArithmetic(string name, IRValueInstruction left, IRValueInstruction right, IRType type, WasmOpCodes i32, WasmOpCodes i64, WasmOpCodes f32, WasmOpCodes f64)
        {
            WasmFormTypes wasmType = type.IsFatPointer ? WasmFormTypes.Void : WasmType(type);

            WasmOpCodes opCode = wasmType switch
            {
                WasmFormTypes.I32 => i32,
                WasmFormTypes.I64 => i64,
                WasmFormTypes.F32 => f32,
                WasmFormTypes.F64 => f64,
                _ => throw ErrorHandler.Create($"{name} does not support operands of type {type.Dump(0)}")
            };

            return [
                .. EmitAs(left, wasmType),
                .. EmitAs(right, wasmType),
                .. Op(opCode),
                .. Normalize(type.DataType)
            ];
        }

        public byte[] EmitDivide(IRDivide divide)
        {
            WasmFormTypes wasmType = divide.ValueType.IsFatPointer ? WasmFormTypes.Void : WasmType(divide.ValueType);

            switch (wasmType)
            {
                case WasmFormTypes.F32:
                case WasmFormTypes.F64:
                    return [
                        .. EmitAs(divide.Left, wasmType),
                        .. EmitAs(divide.Right, wasmType),
                        .. Op(wasmType == WasmFormTypes.F32 ? WasmOpCodes.F32_div : WasmOpCodes.F64_div)
                    ];

                case WasmFormTypes.I32:
                case WasmFormTypes.I64:
                    break;

                default:
                    throw ErrorHandler.Create($"IRDivide does not support operands of type {divide.ValueType.Dump(0)}");
            }

            bool is64 = wasmType == WasmFormTypes.I64;
            bool signed = IsSigned(divide.ValueType.DataType);
            IRLocal divisor = Borrow(is64 ? IRDataTypes.Int64 : IRDataTypes.Int32);

            byte[] bytes = [
                .. EmitAs(divide.Left, wasmType),
                .. EmitAs(divide.Right, wasmType),
                .. LocalTee(divisor),

                // WebAssembly traps on division by zero; panic with the same message as the other targets instead.
                .. LocalGet(divisor),
                .. Op(is64 ? WasmOpCodes.I64_eqz : WasmOpCodes.I32_eqz),
                .. PanicIf(this.FactorizedFunctions.DivideByZeroPanic),

                .. (signed ? SignedDivide(is64, divisor) : Op(is64 ? WasmOpCodes.I64_div_u : WasmOpCodes.I32_div_u)),
                .. Normalize(divide.ValueType.DataType)
            ];

            this.ScratchPool!.Return(divisor);
            return bytes;
        }

        byte[] SignedDivide(bool is64, IRLocal divisor)
        {
            IRLocal dividend = Borrow(is64 ? IRDataTypes.Int64 : IRDataTypes.Int32);
            WasmFormTypes type = is64 ? WasmFormTypes.I64 : WasmFormTypes.I32;
            byte[] Constant(long value) => is64 ? I64Const(value) : I32Const(value);

            byte[] bytes = [
                .. Op(WasmOpCodes.Drop),
                .. LocalSet(dividend),
                .. LocalGet(divisor),
                .. Constant(-1),
                .. Op(is64 ? WasmOpCodes.I64_eq : WasmOpCodes.I32_eq),
                (byte)AddComplexity(WasmOpCodes.If), (byte)type,
                    .. Constant(0),
                    .. LocalGet(dividend),
                    .. Op(is64 ? WasmOpCodes.I64_sub : WasmOpCodes.I32_sub),
                (byte)AddComplexity(WasmOpCodes.Else),
                    .. LocalGet(dividend),
                    .. LocalGet(divisor),
                    .. Op(is64 ? WasmOpCodes.I64_div_s : WasmOpCodes.I32_div_s),
                (byte)AddComplexity(WasmOpCodes.End),
            ];

            this.ScratchPool!.Return(dividend);
            return bytes;
        }

        public byte[] EmitBitwise(IRBitwise bitwise)
        {
            WasmFormTypes wasmType = bitwise.ValueType.IsFatPointer ? WasmFormTypes.Void : WasmType(bitwise.ValueType);

            if (wasmType is not (WasmFormTypes.I32 or WasmFormTypes.I64))
            {
                throw ErrorHandler.Create($"Bitwise operations are not supported on values of type '{bitwise.ValueType.Dump(0)}'.");
            }

            bool is64 = wasmType == WasmFormTypes.I64;
            bool signed = IsSigned(bitwise.ValueType.DataType);

            WasmOpCodes opCode = bitwise.Operator switch
            {
                IRBitwiseOperator.And => is64 ? WasmOpCodes.I64_and : WasmOpCodes.I32_and,
                IRBitwiseOperator.Or => is64 ? WasmOpCodes.I64_or : WasmOpCodes.I32_or,
                IRBitwiseOperator.Xor => is64 ? WasmOpCodes.I64_xor : WasmOpCodes.I32_xor,
                IRBitwiseOperator.ShiftLeft => is64 ? WasmOpCodes.I64_shl : WasmOpCodes.I32_shl,
                IRBitwiseOperator.ShiftRight when signed => is64 ? WasmOpCodes.I64_shr_s : WasmOpCodes.I32_shr_s,
                IRBitwiseOperator.ShiftRight => is64 ? WasmOpCodes.I64_shr_u : WasmOpCodes.I32_shr_u,
                _ => throw ErrorHandler.CreateNotImplimented($"Bitwise operator '{bitwise.Operator}' is not implemented.")
            };

            return [
                .. EmitAs(bitwise.Left, wasmType),
                .. EmitAs(bitwise.Right, wasmType),
                .. Op(opCode),
                .. Normalize(bitwise.ValueType.DataType)
            ];
        }

        public byte[] EmitMath(IRMath math)
        {
            WasmFormTypes wasmType = WasmType(math.ValueType);

            if (math.Operation == IRMathOperation.Reinterpret)
            {
                WasmFormTypes source = WasmType(math.Value.ValueType);

                WasmOpCodes reinterpret = (source, wasmType) switch
                {
                    (WasmFormTypes.F32, WasmFormTypes.I32) => WasmOpCodes.I32_reinterpret_f32,
                    (WasmFormTypes.F64, WasmFormTypes.I64) => WasmOpCodes.I64_reinterpret_f64,
                    (WasmFormTypes.I32, WasmFormTypes.F32) => WasmOpCodes.F32_reinterpret_i32,
                    (WasmFormTypes.I64, WasmFormTypes.F64) => WasmOpCodes.F64_reinterpret_i64,
                    _ => throw ErrorHandler.Create($"Cannot reinterpret {math.Value.ValueType.Dump(0)} as {math.ValueType.Dump(0)}."),
                };

                return [.. EmitAs(math.Value, source), .. Op(reinterpret)];
            }

            WasmOpCodes opCode = (math.Operation, wasmType) switch
            {
                (IRMathOperation.SquareRoot, WasmFormTypes.F32) => WasmOpCodes.F32_sqrt,
                (IRMathOperation.SquareRoot, WasmFormTypes.F64) => WasmOpCodes.F64_sqrt,
                _ => throw ErrorHandler.Create($"'{math.Operation}' does not support operands of type {math.ValueType.Dump(0)}")
            };

            return [.. EmitAs(math.Value, wasmType), .. Op(opCode)];
        }

        public byte[] EmitConvert(IRConvert convert)
        {
            IRType source = convert.Value.ValueType;
            IRType target = convert.ValueType;

            if (source.IsFatPointer)
            {
                // A string or array converts to another reference type unchanged, or to the address of its data.
                return target.IsFatPointer ? EmitInstruction(convert.Value) : EmitAs(convert.Value, WasmType(target));
            }

            if (target.IsFatPointer)
            {
                throw ErrorHandler.CreateNotImplimented($"Converting '{source.Dump(0)}' to '{target.Dump(0)}' is not supported when targeting WebAssembly.");
            }

            WasmFormTypes from = WasmType(source);
            WasmFormTypes to = WasmType(target);
            byte[] value = EmitInstruction(convert.Value);

            if (!IsFloat(from) && !IsFloat(to))
            {
                if (target.DataType == IRDataTypes.Bool)
                {
                    return [.. value, .. Op(from == WasmFormTypes.I64 ? WasmOpCodes.I64_eqz : WasmOpCodes.I32_eqz), .. Op(WasmOpCodes.I32_eqz)];
                }

                return [.. value, .. Coerce(source, to), .. Normalize(target.DataType)];
            }

            if (!IsFloat(from))
            {
                bool signed = IsSigned(source.DataType);

                return [.. value, .. Op((from, to, signed) switch
                {
                    (WasmFormTypes.I32, WasmFormTypes.F32, true) => WasmOpCodes.F32_convert_i32_s,
                    (WasmFormTypes.I32, WasmFormTypes.F32, false) => WasmOpCodes.F32_convert_i32_u,
                    (WasmFormTypes.I64, WasmFormTypes.F32, true) => WasmOpCodes.F32_convert_i64_s,
                    (WasmFormTypes.I64, WasmFormTypes.F32, false) => WasmOpCodes.F32_convert_i64_u,
                    (WasmFormTypes.I32, WasmFormTypes.F64, true) => WasmOpCodes.F64_convert_i32_s,
                    (WasmFormTypes.I32, WasmFormTypes.F64, false) => WasmOpCodes.F64_convert_i32_u,
                    (WasmFormTypes.I64, WasmFormTypes.F64, true) => WasmOpCodes.F64_convert_i64_s,
                    _ => WasmOpCodes.F64_convert_i64_u,
                })];
            }

            if (!IsFloat(to))
            {
                if (target.DataType == IRDataTypes.Bool)
                {
                    // Non-zero (including NaN) is true.
                    return from == WasmFormTypes.F32
                        ? [.. value, (byte)AddComplexity(WasmOpCodes.F32_const), .. BitConverter.GetBytes(0f), .. Op(WasmOpCodes.F32_ne)]
                        : [.. value, (byte)AddComplexity(WasmOpCodes.F64_const), .. BitConverter.GetBytes(0d), .. Op(WasmOpCodes.F64_ne)];
                }

                // Truncate toward zero through 64 bits, then narrow, as the x86-64 target does. The saturating
                // instructions (0xFC 4-7: i64.trunc_sat_f32_s/u, i64.trunc_sat_f64_s/u) never trap.
                ulong subOpCode = (ulong)((from == WasmFormTypes.F32 ? 4 : 6) + (target.DataType == IRDataTypes.UInt64 ? 1 : 0));
                byte[] truncation = [.. value, .. Op(WasmOpCodes.Prefix_FC), .. LEB128.EncodeUnsigned(subOpCode)];

                return to == WasmFormTypes.I64
                    ? truncation
                    : [.. truncation, .. Op(WasmOpCodes.I32_wrap_i64), .. Normalize(target.DataType)];
            }

            return (from, to) switch
            {
                (WasmFormTypes.F32, WasmFormTypes.F64) => [.. value, .. Op(WasmOpCodes.F64_promote_f32)],
                (WasmFormTypes.F64, WasmFormTypes.F32) => [.. value, .. Op(WasmOpCodes.F32_demote_f64)],
                _ => value
            };
        }

        public byte[] EmitLoadLocal(IRLocal local)
        {
            List<byte> bytecode = [];

            bytecode.AddRange(LocalGet(local));

            if (local.ValueType.IsFatPointer)
            {
                if(local.LengthCompanion == null)
                {
                    throw ErrorHandler.Create($"Local load is a reference type but has no length companion.");
                }

                bytecode.AddRange(LocalGet(local.LengthCompanion));
            }

            return bytecode.ToArray();
        }

        public byte[] EmitLoadGlobal(IRGlobal global)
        {
            List<byte> bytecode = [];

            bytecode.AddRange([(byte)AddComplexity(WasmOpCodes.Global_get), .. LEB128.EncodeUnsigned(global.Offset)]);

            if (global.ValueType.IsFatPointer)
            {
                if (global.LengthCompanion == null)
                {
                    throw ErrorHandler.Create($"Global load is a reference type but has no length companion.");
                }

                bytecode.AddRange([(byte)AddComplexity(WasmOpCodes.Global_get), .. LEB128.EncodeUnsigned(global.LengthCompanion.Offset)]);
            }

            return bytecode.ToArray();
        }

        public byte[] EmitLoadString(IRConstantString str)
        {
            List<byte> bytecode = [
                (byte)AddComplexity(WasmOpCodes.I32_const), .. LEB128.EncodeSigned((int)str.Offset),
                (byte)AddComplexity(WasmOpCodes.I32_const), .. LEB128.EncodeSigned(Encoding.UTF8.GetBytes(str.Value).LongLength),
            ];

            return bytecode.ToArray();
        }

        public byte[] EmitProperty(IRStructProperty property)
        {
            return [(byte)AddComplexity(WasmOpCodes.I32_const), .. LEB128.EncodeSigned(property.Offset)];
        }

        /// <summary>
        /// Emits the address a load or store with a target and an optional offset refers to.
        /// Accessing a struct property through a null pointer panics, as on the other targets.
        /// </summary>
        byte[] EmitAddress(IRValueInstruction target, IRValueInstruction? offset)
        {
            List<byte> bytecode = [.. EmitAs(target, WasmFormTypes.I32)];

            if (offset is IRStructProperty)
            {
                IRLocal pointer = Borrow(IRDataTypes.Int32);
                bytecode.AddRange(LocalTee(pointer));
                bytecode.AddRange(Op(WasmOpCodes.I32_eqz));
                bytecode.AddRange(PanicIf(this.FactorizedFunctions.NullReferencePanic));
                bytecode.AddRange(LocalGet(pointer));
                this.ScratchPool!.Return(pointer);
            }

            if (offset != null)
            {
                // A property's ValueType is the type of the value stored there; as an offset it is always an i32.
                bytecode.AddRange(offset is IRStructProperty property ? EmitProperty(property) : EmitAs(offset, WasmFormTypes.I32));
                bytecode.AddRange(Op(WasmOpCodes.I32_add));
            }

            return bytecode.ToArray();
        }

        public byte[] EmitStore(IRStore store)
        {
            List<byte> bytecode = [];

            switch (store.Target)
            {
                case IRLocal local when store.Offset == null:
                    {
                        if(local.ValueType.IsFatPointer)
                        {
                            if(local.LengthCompanion == null)
                            {
                                throw ErrorHandler.Create($"Cannot store to local as its length companion is null.");
                            }

                            bytecode.AddRange(EmitInstruction(store.Value));
                            bytecode.AddRange(LocalSet(local.LengthCompanion));
                        }
                        else
                        {
                            bytecode.AddRange(EmitAs(store.Value, WasmType(local.ValueType)));
                        }

                        bytecode.AddRange(LocalSet(local));

                        return bytecode.ToArray();
                    }

                case IRGlobal global when store.Offset == null:
                    {
                        if (global.ValueType.IsFatPointer)
                        {
                            if (global.LengthCompanion == null)
                            {
                                throw ErrorHandler.Create($"Cannot store to global as its length companion is null.");
                            }

                            bytecode.AddRange(EmitInstruction(store.Value));
                            bytecode.AddRange([(byte)AddComplexity(WasmOpCodes.Global_set), .. LEB128.EncodeUnsigned(global.LengthCompanion.Offset)]);
                        }
                        else
                        {
                            bytecode.AddRange(EmitAs(store.Value, WasmType(global.ValueType)));
                        }

                        bytecode.AddRange([(byte)AddComplexity(WasmOpCodes.Global_set), .. LEB128.EncodeUnsigned(global.Offset)]);

                        return bytecode.ToArray();
                    }
            }

            // Otherwise the target is a pointer and the value is written to memory.
            bytecode.AddRange(EmitAddress(store.Target, store.Offset));
            bytecode.AddRange(StoreToAddress(store.Value, store.Value.ValueType));

            return bytecode.ToArray();
        }

        public byte[] EmitLoad(IRLoad load)
        {
            if (load.Offset == null)
            {
                switch (load.Target)
                {
                    case IRLocal local:
                        return EmitLoadLocal(local);

                    case IRGlobal global:
                        return EmitLoadGlobal(global);
                }
            }

            // Otherwise the target is a pointer and the value is read from memory.
            return [.. EmitAddress(load.Target, load.Offset), .. LoadFromAddress(load.TargetType)];
        }

        /// <summary>
        /// Emits the address of an array element, after checking the index against the array's length.
        /// </summary>
        byte[] EmitElementAddress(IRValueInstruction array, IRValueInstruction index, IRType elementType)
        {
            if (!array.ValueType.IsFatPointer)
            {
                throw ErrorHandler.Create($"Expected an array or a string, but got '{array.ValueType.Dump(0)}'.");
            }

            IRLocal pointer = Borrow(IRDataTypes.Int32);
            IRLocal length = Borrow(IRDataTypes.Int32);
            IRLocal position = Borrow(IRDataTypes.Int32);
            bool wideIndex = !index.ValueType.IsFatPointer && WasmType(index.ValueType) == WasmFormTypes.I64;
            IRLocal? wide = wideIndex ? Borrow(IRDataTypes.Int64) : null;
            byte[] target = EmitInstruction(array);

            byte[] check = wide != null
                ? [
                    .. EmitInstruction(index),
                    .. LocalTee(wide),
                    .. I64Const(32),
                    .. Op(WasmOpCodes.I64_shr_u),
                    .. Op(WasmOpCodes.I64_eqz),
                    .. Op(WasmOpCodes.I32_eqz),
                    .. LocalGet(wide),
                    .. Op(WasmOpCodes.I32_wrap_i64),
                    .. LocalTee(position),
                    .. LocalGet(length),
                    .. Op(WasmOpCodes.I32_ge_u),
                    .. Op(WasmOpCodes.I32_or),
                ]
                : [
                    .. EmitAs(index, WasmFormTypes.I32),
                    .. LocalTee(position),
                    .. LocalGet(length),
                    .. Op(WasmOpCodes.I32_ge_u),
                ];

            byte[] bytes = [
                .. target,
                .. LocalSet(length),
                .. LocalSet(pointer),

                // An unsigned comparison also catches negative indices.
                .. check,
                .. PanicIf(this.FactorizedFunctions.IndexOutOfRangePanic),

                .. LocalGet(pointer),
                .. LocalGet(position),
                .. I32Const(ElementWidth(elementType)),
                .. Op(WasmOpCodes.I32_mul),
                .. Op(WasmOpCodes.I32_add),
            ];

            if (wide != null)
            {
                this.ScratchPool!.Return(wide);
            }

            this.ScratchPool!.Return(position);
            this.ScratchPool!.Return(length);
            this.ScratchPool!.Return(pointer);
            return bytes;
        }

        public byte[] EmitLoadElement(IRLoadElement loadElement)
        {
            if (IRStructSchema.IsInline(loadElement.ElementType))
            {
                return EmitElementAddress(loadElement.Target, loadElement.Index, loadElement.ElementType);
            }

            return [.. EmitElementAddress(loadElement.Target, loadElement.Index, loadElement.ElementType), .. LoadFromAddress(loadElement.ElementType)];
        }

        public byte[] EmitStoreElement(IRStoreElement storeElement)
        {
            if (IRStructSchema.IsInline(storeElement.ElementType))
            {
                return [.. EmitElementAddress(storeElement.Target, storeElement.Index, storeElement.ElementType), .. CopyStructToAddress(storeElement.Value, (IRStructSchema)storeElement.ElementType.UserObject!)];
            }

            return [.. EmitElementAddress(storeElement.Target, storeElement.Index, storeElement.ElementType), .. StoreToAddress(storeElement.Value, storeElement.ElementType)];
        }

        /// <summary>
        /// Copies the struct <paramref name="value"/> points to into the address on top of the stack.
        /// </summary>
        byte[] CopyStructToAddress(IRValueInstruction value, IRStructSchema schema)
        {
            IRLocal destination = Borrow(IRDataTypes.Int32);
            IRLocal source = Borrow(IRDataTypes.Int32);
            List<byte> bytes = [
                .. LocalSet(destination),
                .. EmitAs(value, WasmFormTypes.I32),
                .. LocalTee(source),
                .. Op(WasmOpCodes.I32_eqz),
                .. PanicIf(this.FactorizedFunctions.NullReferencePanic),
            ];

            int offset = 0;

            foreach ((int size, WasmOpCodes load, WasmOpCodes store) in new[] { (8, WasmOpCodes.I64_load, WasmOpCodes.I64_store), (4, WasmOpCodes.I32_load, WasmOpCodes.I32_store), (2, WasmOpCodes.I32_load16_u, WasmOpCodes.I32_store16), (1, WasmOpCodes.I32_load8_u, WasmOpCodes.I32_store8) })
            {
                while (schema.Width - offset >= size)
                {
                    bytes.AddRange(LocalGet(destination));
                    bytes.AddRange(LocalGet(source));
                    bytes.AddRange(MemoryAccess(load, (ulong)offset));
                    bytes.AddRange(MemoryAccess(store, (ulong)offset));
                    offset += size;
                }
            }

            this.ScratchPool!.Return(source);
            this.ScratchPool!.Return(destination);
            return bytes.ToArray();
        }

        public byte[] EmitLength(IRLength length)
        {
            if (!length.Target.ValueType.IsFatPointer)
            {
                throw ErrorHandler.Create($"Expected an array or a string, but got '{length.Target.ValueType.Dump(0)}'.");
            }

            IRLocal scratch = Borrow(IRDataTypes.Int32);

            byte[] bytes = [
                .. EmitInstruction(length.Target),
                .. LocalSet(scratch),
                .. Op(WasmOpCodes.Drop),
                .. LocalGet(scratch),
                .. Coerce(new IRType(IRDataTypes.UInt32), WasmType(length.ValueType)),
            ];

            this.ScratchPool!.Return(scratch);
            return bytes;
        }

        public byte[] EmitFatPointer(IRFatPointer fatPointer)
        {
            return [.. EmitAs(fatPointer.Pointer, WasmFormTypes.I32), .. EmitAs(fatPointer.Length, WasmFormTypes.I32)];
        }

        public byte[] EmitReturn(IRReturn ret)
        {
            List<byte> bytes = new List<byte>();

            if(ret.Values != null)
            {
                for (int i = 0; i < ret.Values.Count; i++)
                {
                    IRValueInstruction value = ret.Values[i];
                    IRType? returnType = this.Function != null && i < this.Function.ReturnTypes.Count ? this.Function.ReturnTypes[i] : null;

                    bytes.AddRange(returnType == null || returnType.IsFatPointer || value.ValueType.IsFatPointer
                        ? EmitInstruction(value)
                        : EmitAs(value, WasmType(returnType)));
                }
            }

            return [
                .. bytes,
                (byte)AddComplexity(WasmOpCodes.Return)
            ];
        }

        /// <summary>
        /// Emits a constant. A string or array constant is a fat pointer, so it also pushes a zero length.
        /// </summary>
        public byte[] EmitConstant(IRConstantInteger constInt)
        {
            byte[] value = EmitScalarConstant(constInt.IntegerType, constInt.Value);
            return constInt.ValueType.IsFatPointer ? [.. value, .. I32Const(0)] : value;
        }

        public byte[] EmitScalarConstant(IRDataTypes dataType, double value)
        {
            WasmFormTypes wasmType = WasmTypeTranslator.TranslateIRDataType(dataType);

            return wasmType switch
            {
                WasmFormTypes.I32 => [(byte)AddComplexity(WasmOpCodes.I32_const), .. LEB128.EncodeSigned(unchecked((int)IntegerConstant(dataType, value)))],
                WasmFormTypes.I64 => [(byte)AddComplexity(WasmOpCodes.I64_const), .. LEB128.EncodeSigned(IntegerConstant(dataType, value))],
                WasmFormTypes.F32 => [(byte)AddComplexity(WasmOpCodes.F32_const), .. BitConverter.GetBytes((float)value)],
                WasmFormTypes.F64 => [(byte)AddComplexity(WasmOpCodes.F64_const), .. BitConverter.GetBytes(value)],
                _ => throw ErrorHandler.Create($"Cannot emit constant of type {dataType}")
            };
        }

        /// <summary>
        /// The bits of an integer constant: the value truncated to its type, as values of that type are kept at runtime.
        /// Constants are not always in range, for example when a conversion of a literal is folded into a constant.
        /// </summary>
        static long IntegerConstant(IRDataTypes dataType, double value)
        {
            if (dataType is IRDataTypes.UInt64 && value >= 9223372036854775808.0)
            {
                return unchecked((long)(ulong)value);
            }

            long integer = (long)value;

            return dataType switch
            {
                IRDataTypes.Bool => integer != 0 ? 1 : 0,
                IRDataTypes.Int8 => (sbyte)integer,
                IRDataTypes.UInt8 => (byte)integer,
                IRDataTypes.Int16 => (short)integer,
                IRDataTypes.UInt16 => (ushort)integer,
                IRDataTypes.Int32 => (int)integer,
                IRDataTypes.UInt32 => (uint)integer,
                _ => integer,
            };
        }

        public byte[] EmitCall(IRCall call)
        {
            List<byte> bytes = [];

            for (int i = 0; i < call.Arguments.Count; i++)
            {
                IRValueInstruction argument = call.Arguments[i];
                IRType? parameterType = i < call.Function.Parameters.Count ? call.Function.Parameters[i].ValueType : null;

                bytes.AddRange(parameterType == null || parameterType.IsFatPointer || argument.ValueType.IsFatPointer
                    ? EmitInstruction(argument)
                    : EmitAs(argument, WasmType(parameterType)));
            }

            return [.. bytes, .. Call(call.Function)];
        }

        /// <summary>
        /// Calls the function whose table index is the function pointer. The type comes from the arguments and the return type.
        /// </summary>
        public byte[] EmitCallIndirect(IRCallIndirect call)
        {
            List<byte> bytes = [];
            List<WasmFormTypes> parameterTypes = [];

            foreach (IRValueInstruction argument in call.Arguments)
            {
                bytes.AddRange(EmitInstruction(argument));
                parameterTypes.AddRange(argument.ValueType.IsFatPointer ? [WasmFormTypes.I32, WasmFormTypes.I32] : [WasmType(argument.ValueType)]);
            }

            List<WasmFormTypes> resultTypes = call.ValueType.DataType == IRDataTypes.Void ? []
                : call.ValueType.IsFatPointer ? [WasmFormTypes.I32, WasmFormTypes.I32]
                : [WasmType(call.ValueType)];

            return [
                .. bytes,
                .. EmitAs(call.FunctionPointer, WasmFormTypes.I32),
                (byte)AddComplexity(WasmOpCodes.Call_indirect),
                .. LEB128.EncodeUnsigned(this.FactorizedFunctions.TypeIndexOf(parameterTypes, resultTypes)),
                0x00,
            ];
        }

        public byte[] EmitBlock(IRBlock block)
        {
            List<byte> bytes = [
                (byte)AddComplexity(WasmOpCodes.Block),
                (byte)WasmTypeTranslator.TranslateIRType(block.ReturnType)
            ];

            LabelStack.Add(WasmLabelKinds.Block);
            BlockContextStack.Push(block);
            bytes.AddRange(EmitInstructions(block.Instructions));
            ulong complexity = BlockContextStack.Peek().SpeculativeCircuitComplexity;
            BlockContextStack.Pop();
            BlockContextStack.Peek().SpeculativeCircuitComplexity += complexity;
            LabelStack.RemoveAt(LabelStack.Count - 1);

            bytes.Add((byte)AddComplexity(WasmOpCodes.End));

            return bytes.ToArray();
        }

        List<WasmLabelKinds> LabelStack { get; set; } = new List<WasmLabelKinds>();

        enum WasmLabelKinds
        {
            Block,
            If,
            LoopExit,
            LoopTop,
            LoopContinue,
        }

        ulong LabelDepth(WasmLabelKinds kind, string instructionName)
        {
            int index = LabelStack.LastIndexOf(kind);

            if (index == -1)
            {
                throw ErrorHandler.Create($"'{instructionName}' can only be used inside a loop.");
            }

            return (ulong)(LabelStack.Count - 1 - index);
        }

        public byte[] EmitBreak()
        {
            return [(byte)AddComplexity(WasmOpCodes.Br), .. LEB128.EncodeUnsigned(LabelDepth(WasmLabelKinds.LoopExit, "break"))];
        }

        public byte[] EmitContinue()
        {
            return [(byte)AddComplexity(WasmOpCodes.Br), .. LEB128.EncodeUnsigned(LabelDepth(WasmLabelKinds.LoopContinue, "continue"))];
        }

        public byte[] EmitBlockNoWrap(IRBlock block)
        {
            List<byte> bytes = [];
            BlockContextStack.Push(block);
            bytes.AddRange(EmitInstructions(block.Instructions));
            ulong complexity = BlockContextStack.Peek().SpeculativeCircuitComplexity;
            BlockContextStack.Pop();
            BlockContextStack.Peek().SpeculativeCircuitComplexity += complexity;
            return bytes.ToArray();
        }

        public byte[] EmitConditionalBranch(IRConditionalBranch conditionalBranch)
        {
            if (IRSwitchAnalysis.Find(conditionalBranch) is IRSwitch jumpTable)
            {
                return EmitJumpTable(jumpTable);
            }

            List<byte> bytes = [];

            bytes.AddRange(EmitAs(conditionalBranch.Condition, WasmFormTypes.I32));

            LabelStack.Add(WasmLabelKinds.If);

            bytes.AddRange([
                (byte)AddComplexity(WasmOpCodes.If),
                (byte)WasmTypeTranslator.TranslateIRType(conditionalBranch.ThenBlock.ReturnType),
                .. EmitBlockNoWrap(conditionalBranch.ThenBlock),
            ]);

            if (conditionalBranch.HasElseBlock)
            {
                bytes.Add((byte)AddComplexity(WasmOpCodes.Else));
                bytes.AddRange(EmitBlockNoWrap(conditionalBranch.ElseBlock));
            }

            LabelStack.RemoveAt(LabelStack.Count - 1);

            bytes.Add((byte)AddComplexity(WasmOpCodes.End));

            return bytes.ToArray();
        }

        byte[] EmitJumpTable(IRSwitch jumpTable)
        {
            List<byte> bytes = [];
            int cases = jumpTable.Cases.Count;
            int range = jumpTable.Slots.Length;
            byte empty = (byte)WasmFormTypes.Void;
            int endIndex = LabelStack.Count;

            for (int i = 0; i < cases + 2; i++)
            {
                LabelStack.Add(WasmLabelKinds.Block);
                bytes.AddRange([(byte)AddComplexity(WasmOpCodes.Block), empty]);
            }

            bool wide = jumpTable.Subject.ValueType.DataType is IRDataTypes.Int64 or IRDataTypes.UInt64;

            if (wide)
            {
                bytes.AddRange([
                    .. EmitAs(jumpTable.Subject, WasmFormTypes.I64),
                    (byte)AddComplexity(WasmOpCodes.I64_const), .. LEB128.EncodeSigned(jumpTable.Minimum),
                    (byte)AddComplexity(WasmOpCodes.I64_sub),
                    (byte)AddComplexity(WasmOpCodes.I64_const), .. LEB128.EncodeSigned(range),
                    (byte)AddComplexity(WasmOpCodes.I64_lt_u),
                    (byte)AddComplexity(WasmOpCodes.If), (byte)WasmFormTypes.I32,
                    .. EmitAs(jumpTable.Subject, WasmFormTypes.I64),
                    (byte)AddComplexity(WasmOpCodes.I64_const), .. LEB128.EncodeSigned(jumpTable.Minimum),
                    (byte)AddComplexity(WasmOpCodes.I64_sub),
                    (byte)AddComplexity(WasmOpCodes.I32_wrap_i64),
                    (byte)AddComplexity(WasmOpCodes.Else),
                    .. I32Const(range),
                    (byte)AddComplexity(WasmOpCodes.End),
                ]);
            }
            else
            {
                bytes.AddRange([
                    .. EmitAs(jumpTable.Subject, WasmFormTypes.I32),
                    .. I32Const((int)jumpTable.Minimum),
                    (byte)AddComplexity(WasmOpCodes.I32_sub),
                ]);
            }

            ulong Target(int slot) => slot < 0 ? (ulong)cases : jumpTable.Cases[slot] == null ? (ulong)cases + 1 : (ulong)slot;

            bytes.Add((byte)AddComplexity(WasmOpCodes.Br_table));
            bytes.AddRange(LEB128.EncodeUnsigned((ulong)range));

            foreach (int slot in jumpTable.Slots)
            {
                bytes.AddRange(LEB128.EncodeUnsigned(Target(slot)));
            }

            bytes.AddRange(LEB128.EncodeUnsigned((ulong)cases));

            for (int i = 0; i < cases; i++)
            {
                LabelStack.RemoveAt(LabelStack.Count - 1);
                bytes.Add((byte)AddComplexity(WasmOpCodes.End));

                if (jumpTable.Cases[i] is IRBlock body)
                {
                    bytes.AddRange(EmitBlockNoWrap(body));

                    if (!IRTree.AlwaysExits(body))
                    {
                        bytes.Add((byte)AddComplexity(WasmOpCodes.Br));
                        bytes.AddRange(LEB128.EncodeUnsigned((ulong)(LabelStack.Count - 1 - endIndex)));
                    }
                }
            }

            LabelStack.RemoveAt(LabelStack.Count - 1);
            bytes.Add((byte)AddComplexity(WasmOpCodes.End));

            if (jumpTable.Default != null)
            {
                bytes.AddRange(EmitBlockNoWrap(jumpTable.Default));
            }

            LabelStack.RemoveAt(LabelStack.Count - 1);
            bytes.Add((byte)AddComplexity(WasmOpCodes.End));

            return bytes.ToArray();
        }

        public byte[] EmitLoop(IRLoop loop)
        {
            List<byte> bytes = [];
            byte blockType = (byte)WasmTypeTranslator.TranslateIRType(loop.Block.ReturnType);

            LabelStack.AddRange([WasmLabelKinds.LoopExit, WasmLabelKinds.LoopTop]);

            bytes.AddRange([
                (byte)AddComplexity(WasmOpCodes.Block),
                blockType,

                (byte)AddComplexity(WasmOpCodes.Loop),
                blockType,

                .. EmitAs(loop.Condition, WasmFormTypes.I32),

                (byte)AddComplexity(WasmOpCodes.I32_eqz),
                (byte)AddComplexity(WasmOpCodes.Br_if),
                .. LEB128.EncodeUnsigned(1),

                (byte)AddComplexity(WasmOpCodes.Block),
                blockType,
            ]);

            LabelStack.Add(WasmLabelKinds.LoopContinue);
            bytes.AddRange(EmitBlockNoWrap(loop.Block));
            LabelStack.RemoveAt(LabelStack.Count - 1);

            bytes.Add((byte)AddComplexity(WasmOpCodes.End));

            if (loop.Increment != null)
            {
                bytes.AddRange(EmitBlockNoWrap(loop.Increment));
            }

            bytes.AddRange([
                (byte)AddComplexity(WasmOpCodes.Br),
                .. LEB128.EncodeUnsigned(0),

                (byte)AddComplexity(WasmOpCodes.End),
                (byte)AddComplexity(WasmOpCodes.End)
            ]);

            LabelStack.RemoveRange(LabelStack.Count - 2, 2);

            return bytes.ToArray();
        }

        public byte[] EmitCompare(IRCompare compare)
        {
            if (compare.OperandType.IsFatPointer)
            {
                return EmitFatCompare(compare);
            }

            WasmFormTypes operandType = WasmType(compare.OperandType);
            bool signed = IsSigned(compare.OperandType.DataType);

            WasmOpCodes comparisonInstruction = (operandType, compare.Operator) switch
            {
                (WasmFormTypes.I32, IRComparisonOperator.EqualTo) => WasmOpCodes.I32_eq,
                (WasmFormTypes.I32, IRComparisonOperator.NotEqualTo) => WasmOpCodes.I32_ne,
                (WasmFormTypes.I32, IRComparisonOperator.LessThan) => signed ? WasmOpCodes.I32_lt_s : WasmOpCodes.I32_lt_u,
                (WasmFormTypes.I32, IRComparisonOperator.GreaterThan) => signed ? WasmOpCodes.I32_gt_s : WasmOpCodes.I32_gt_u,
                (WasmFormTypes.I32, IRComparisonOperator.LessThanOrEqual) => signed ? WasmOpCodes.I32_le_s : WasmOpCodes.I32_le_u,
                (WasmFormTypes.I32, IRComparisonOperator.GreaterThanOrEqual) => signed ? WasmOpCodes.I32_ge_s : WasmOpCodes.I32_ge_u,

                (WasmFormTypes.I64, IRComparisonOperator.EqualTo) => WasmOpCodes.I64_eq,
                (WasmFormTypes.I64, IRComparisonOperator.NotEqualTo) => WasmOpCodes.I64_ne,
                (WasmFormTypes.I64, IRComparisonOperator.LessThan) => signed ? WasmOpCodes.I64_lt_s : WasmOpCodes.I64_lt_u,
                (WasmFormTypes.I64, IRComparisonOperator.GreaterThan) => signed ? WasmOpCodes.I64_gt_s : WasmOpCodes.I64_gt_u,
                (WasmFormTypes.I64, IRComparisonOperator.LessThanOrEqual) => signed ? WasmOpCodes.I64_le_s : WasmOpCodes.I64_le_u,
                (WasmFormTypes.I64, IRComparisonOperator.GreaterThanOrEqual) => signed ? WasmOpCodes.I64_ge_s : WasmOpCodes.I64_ge_u,

                (WasmFormTypes.F32, IRComparisonOperator.EqualTo) => WasmOpCodes.F32_eq,
                (WasmFormTypes.F32, IRComparisonOperator.NotEqualTo) => WasmOpCodes.F32_ne,
                (WasmFormTypes.F32, IRComparisonOperator.LessThan) => WasmOpCodes.F32_lt,
                (WasmFormTypes.F32, IRComparisonOperator.GreaterThan) => WasmOpCodes.F32_gt,
                (WasmFormTypes.F32, IRComparisonOperator.LessThanOrEqual) => WasmOpCodes.F32_le,
                (WasmFormTypes.F32, IRComparisonOperator.GreaterThanOrEqual) => WasmOpCodes.F32_ge,

                (WasmFormTypes.F64, IRComparisonOperator.EqualTo) => WasmOpCodes.F64_eq,
                (WasmFormTypes.F64, IRComparisonOperator.NotEqualTo) => WasmOpCodes.F64_ne,
                (WasmFormTypes.F64, IRComparisonOperator.LessThan) => WasmOpCodes.F64_lt,
                (WasmFormTypes.F64, IRComparisonOperator.GreaterThan) => WasmOpCodes.F64_gt,
                (WasmFormTypes.F64, IRComparisonOperator.LessThanOrEqual) => WasmOpCodes.F64_le,
                (WasmFormTypes.F64, IRComparisonOperator.GreaterThanOrEqual) => WasmOpCodes.F64_ge,

                _ => throw ErrorHandler.CreateNotImplimented($"Instruction '{compare.Operator}' for type '{compare.OperandType.DataType}' is not yet implemented.")
            };

            return [
                .. EmitAs(compare.Left, operandType),
                .. EmitAs(compare.Right, operandType),
                (byte)AddComplexity(comparisonInstruction),
            ];
        }

        /// <summary>
        /// Strings and arrays are equal when both their pointers and their lengths are, like on the x86-64 target.
        /// </summary>
        byte[] EmitFatCompare(IRCompare compare)
        {
            if (compare.Operator is not (IRComparisonOperator.EqualTo or IRComparisonOperator.NotEqualTo))
            {
                throw ErrorHandler.Create($"Only equality comparisons are supported on values of type '{compare.OperandType.Dump(0)}'.");
            }

            IRLocal leftPointer = Borrow(IRDataTypes.Int32);
            IRLocal leftLength = Borrow(IRDataTypes.Int32);
            IRLocal sameLength = Borrow(IRDataTypes.Int32);

            byte[] bytes = [
                .. EmitInstruction(compare.Left),
                .. LocalSet(leftLength),
                .. LocalSet(leftPointer),

                .. EmitInstruction(compare.Right),
                .. LocalGet(leftLength),
                .. Op(WasmOpCodes.I32_eq),
                .. LocalSet(sameLength),
                .. LocalGet(leftPointer),
                .. Op(WasmOpCodes.I32_eq),
                .. LocalGet(sameLength),
                .. Op(WasmOpCodes.I32_and),
                .. (compare.Operator == IRComparisonOperator.NotEqualTo ? Op(WasmOpCodes.I32_eqz) : []),
            ];

            this.ScratchPool!.Return(sameLength);
            this.ScratchPool!.Return(leftLength);
            this.ScratchPool!.Return(leftPointer);
            return bytes;
        }
    }
}
